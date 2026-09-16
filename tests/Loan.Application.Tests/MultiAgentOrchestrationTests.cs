using Loan.Application.Abstractions;
using Loan.Application.Agents;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class MultiAgentOrchestrationTests
{
    private class InMemoryAppRepo : ILoanApplicationRepository
    {
        private readonly Dictionary<string, LoanApplication> _apps = new();

        public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
        {
            _apps.TryGetValue(applicationId, out var app);
            return Task.FromResult(app);
        }

        public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.ApplicantId == applicantId));
        }

        public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.Status == ApplicationStatus.UnderOfficerReview));
        }
    }

    private class InMemoryRecRepo : IRecommendationRepository
    {
        public readonly Dictionary<string, Recommendation> Recs = new();

        public Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default)
        {
            Recs.TryGetValue(recommendationId, out var r);
            return Task.FromResult(r);
        }

        public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default)
        {
            Recs[recommendation.RecommendationId] = recommendation;
            return Task.CompletedTask;
        }
    }

    private InMemoryAppRepo _appRepo = null!;
    private InMemoryRecRepo _recRepo = null!;
    private SyntheticChatModel _chatModel = null!;
    private SyntheticPolicyRetriever _policyRetriever = null!;
    private RecommendationOrchestratorAgent _orchestrator = null!;
    private EvaluateEligibilityCommandHandler _evalHandler = null!;

    [SetUp]
    public void SetUp()
    {
        _appRepo = new InMemoryAppRepo();
        _recRepo = new InMemoryRecRepo();
        _chatModel = new SyntheticChatModel();
        _policyRetriever = new SyntheticPolicyRetriever();

        var docAgent = new DocumentAnalysisAgent(_chatModel);
        var eligAgent = new EligibilityAnalysisAgent(_chatModel);
        var compAgent = new ComplianceReviewAgent(_policyRetriever, _chatModel);
        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);

        _orchestrator = new RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, saveDraftHandler, _chatModel);

        var identityService = new SyntheticIdentityService();
        var incomeService = new SyntheticIncomeService();
        var creditService = new SyntheticCreditService();
        _evalHandler = new EvaluateEligibilityCommandHandler(_appRepo, identityService, incomeService, creditService);
    }

    [Test]
    public async Task EligibilityAgent_CannotAlterDeterministicRatios_PreservesDomainIndicators()
    {
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(2500m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetIncomeVerified(true, new Money(12000m), DateTime.UtcNow);
        facts.SetCreditVerified(true, 750, DateTime.UtcNow);

        var app = new LoanApplication("APP-TEST-001", "APP-100", rules, facts, DateTime.UtcNow);
        await _appRepo.AddAsync(app);

        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-TEST-001"));

        var agent = new EligibilityAnalysisAgent(_chatModel);
        var result = await agent.AnalyzeAsync(app, app.Indicators!);

        Assert.That(result.DebtToIncomeRatio, Is.EqualTo(app.Indicators!.DebtToIncomeRatio));
        Assert.That(result.LoanToValueRatio, Is.EqualTo(app.Indicators.LoanToValueRatio));
        Assert.That(result.DeterministicStatus, Is.EqualTo(app.Indicators.Status.ToString()));
    }

    [Test]
    public async Task Orchestrator_CannotCreateApproveOrRejectDecisions_StatusIsDraftPreparedBySystem()
    {
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(2500m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetIncomeVerified(true, new Money(12000m), DateTime.UtcNow);
        facts.SetCreditVerified(true, 750, DateTime.UtcNow);

        var app = new LoanApplication("APP-TEST-002", "APP-100", rules, facts, DateTime.UtcNow);
        await _appRepo.AddAsync(app);
        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-TEST-002"));

        var recDto = await _orchestrator.ProcessApplicationAsync(app);

        Assert.That(recDto.Status, Is.EqualTo(RecommendationStatus.DraftPreparedBySystem));
        Assert.That(recDto.ApprovedByOfficerId, Is.Null); // Exclusivity check: NOT approved by officer
    }

    [TestCase("SYN-888777", "MORTGAGE", 12000, 2500, 350000, 500000, 750, "ReadyForOfficerReview", 0.15)]
    [TestCase("SYN-300400", "MORTGAGE", 11000, 5200, 350000, 500000, 690, "ManualReview", 0.65)]
    [TestCase("SYN-000000", "MORTGAGE", 0, 0, 0, 0, 520, "PendingInformation", 0.50)]
    public async Task Orchestrator_DeterministicRoutingStateOverride_LocksRoutingStateAndRiskScore(
        string syntheticId,
        string productType,
        decimal income,
        decimal debts,
        decimal loanAmount,
        decimal propValue,
        int creditScore,
        string expectedRoutingState,
        double expectedRiskScore)
    {
        var rules = productType == "MORTGAGE" ? ProductRules.CreateStandardMortgage("v1.2") : ProductRules.CreatePersonalLoan("v2.0");
        var facts = new ApplicantFacts("APP-100", "Test User", syntheticId, new Money(income), new Money(debts), new Money(loanAmount), new Money(propValue), creditScore, "Employed", "Purchase");

        var appId = $"APP-ORCH-{syntheticId}";
        var app = new LoanApplication(appId, "APP-100", rules, facts, DateTime.UtcNow);
        await _appRepo.AddAsync(app);

        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand(appId));
        var updatedApp = await _appRepo.GetByIdAsync(appId);

        var recDto = await _orchestrator.ProcessApplicationAsync(updatedApp!);

        Assert.That(recDto.RiskScore, Is.EqualTo(expectedRiskScore));
        Assert.That(recDto.Status, Is.EqualTo(RecommendationStatus.DraftPreparedBySystem));
    }

    [Test]
    public async Task Orchestrator_WhenLowConfidenceDocumentUnconfirmed_GatesRoutingToPendingInformation()
    {
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Bob Brown", "SYN-888777", new Money(9500m), new Money(2500m), new Money(300000m), new Money(450000m), 720, "Employed", "Purchase");
        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetCreditVerified(true, 720, DateTime.UtcNow);

        var appId = "APP-TEST-LOWCONF-001";
        var app = new LoanApplication(appId, "APP-100", rules, facts, DateTime.UtcNow);

        // Add verified identity and bank statement
        var idDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-ID-1", appId, "id.txt", "text/plain", 100, "blob-id", "hash1", Loan.Domain.Documents.DocumentType.DriverLicenseOrPassport, DateTime.UtcNow);
        idDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("FullName", "Bob Brown", "Bob Brown", 0.98f, "DOC-ID-1", "header", false, true, Loan.Domain.Documents.FieldConfirmationStatus.ConfirmedByApplicant) });
        app.AddDocumentRecord(idDoc, DateTime.UtcNow);

        var bankDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-BANK-1", appId, "bank.txt", "text/plain", 100, "blob-bank", "hash2", Loan.Domain.Documents.DocumentType.BankStatement, DateTime.UtcNow);
        bankDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("AverageMonthlyBalance", "50000.00", "50000.00", 0.95f, "DOC-BANK-1", "summary", false, true, Loan.Domain.Documents.FieldConfirmationStatus.ConfirmedByApplicant) });
        app.AddDocumentRecord(bankDoc, DateTime.UtcNow);

        // Add smudged paystub with 72% confidence (NeedsConfirmation = true)
        var smudgedDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-PAY-1", appId, "smudged_paystub.txt", "text/plain", 100, "blob-pay", "hash3", Loan.Domain.Documents.DocumentType.Paystub, DateTime.UtcNow);
        smudgedDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("MonthlyGrossIncome", "9500.00", "9500.00", 0.72f, "DOC-PAY-1", "gross pay", false, true, Loan.Domain.Documents.FieldConfirmationStatus.Unconfirmed) });
        app.AddDocumentRecord(smudgedDoc, DateTime.UtcNow);

        await _appRepo.AddAsync(app);
        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand(appId));

        var updatedApp = await _appRepo.GetByIdAsync(appId);
        var recDto = await _orchestrator.ProcessApplicationAsync(updatedApp!);

        // Must be gated to PendingInformation due to unresolved low-confidence field
        Assert.That(recDto.DecisionRecommendation, Is.EqualTo(RecommendationType.PendingInformation));
        Assert.That(recDto.MissingEvidenceItems.Any(i => i.Contains("MonthlyGrossIncome")), Is.True);
        Assert.That(updatedApp!.Status, Is.EqualTo(ApplicationStatus.InformationRequested));
    }

    [Test]
    public async Task Orchestrator_WhenLowConfidenceFieldConfirmed_AdvancesToOfficerReviewAndUpdatesFactSet()
    {
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Bob Brown", "SYN-888777", new Money(9500m), new Money(2500m), new Money(300000m), new Money(450000m), 720, "Employed", "Purchase");
        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetCreditVerified(true, 720, DateTime.UtcNow);

        var appId = "APP-TEST-CONFIRM-002";
        var app = new LoanApplication(appId, "APP-100", rules, facts, DateTime.UtcNow);

        var idDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-ID-2", appId, "id.txt", "text/plain", 100, "blob-id", "hash1", Loan.Domain.Documents.DocumentType.DriverLicenseOrPassport, DateTime.UtcNow);
        idDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("FullName", "Bob Brown", "Bob Brown", 0.98f, "DOC-ID-2", "header", false, true, Loan.Domain.Documents.FieldConfirmationStatus.ConfirmedByApplicant) });
        app.AddDocumentRecord(idDoc, DateTime.UtcNow);

        var bankDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-BANK-2", appId, "bank.txt", "text/plain", 100, "blob-bank", "hash2", Loan.Domain.Documents.DocumentType.BankStatement, DateTime.UtcNow);
        bankDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("AverageMonthlyBalance", "50000.00", "50000.00", 0.95f, "DOC-BANK-2", "summary", false, true, Loan.Domain.Documents.FieldConfirmationStatus.ConfirmedByApplicant) });
        app.AddDocumentRecord(bankDoc, DateTime.UtcNow);

        var smudgedDoc = new Loan.Domain.Documents.ExtractedDocumentRecord("DOC-PAY-2", appId, "smudged_paystub.txt", "text/plain", 100, "blob-pay", "hash3", Loan.Domain.Documents.DocumentType.Paystub, DateTime.UtcNow);
        smudgedDoc.AddExtractedFields(new[] { new Loan.Domain.Documents.ExtractedFieldRecord("MonthlyGrossIncome", "9500.00", "9500.00", 0.72f, "DOC-PAY-2", "gross pay", false, true, Loan.Domain.Documents.FieldConfirmationStatus.Unconfirmed) });
        app.AddDocumentRecord(smudgedDoc, DateTime.UtcNow);

        // Confirm field by applicant
        app.ConfirmOrOverrideField("DOC-PAY-2", "MonthlyGrossIncome", "9500.00", "APP-100", "Applicant", "Confirmed from paper stub", "CORR-1", DateTime.UtcNow);

        // Verify domain Confirmed Fact Set updated
        Assert.That(app.Facts.IsIncomeVerified, Is.True);
        Assert.That(app.Facts.EffectiveMonthlyIncome.Amount, Is.EqualTo(9500.00m));

        await _appRepo.AddAsync(app);
        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand(appId));

        var updatedApp = await _appRepo.GetByIdAsync(appId);
        var recDto = await _orchestrator.ProcessApplicationAsync(updatedApp!);

        // With all items confirmed and valid metrics, should advance to Approve / ReadyForOfficerReview
        Assert.That(recDto.DecisionRecommendation, Is.EqualTo(RecommendationType.Approve));
        Assert.That(updatedApp!.Status, Is.EqualTo(ApplicationStatus.UnderOfficerReview));
    }
}
