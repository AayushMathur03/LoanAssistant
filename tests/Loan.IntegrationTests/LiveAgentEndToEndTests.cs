using Loan.Application.Abstractions;
using Loan.Application.Agents;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Documents;
using Loan.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.IntegrationTests;

[TestFixture]
public class LiveAgentEndToEndTests
{
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Loan.Web");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("LoanAssistant-Web-87612345-EF89")
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public async Task LiveAzure_SeedAllPolicies_AndRunMultiAgentSpecialistsEndToEnd()
    {
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(openAiEndpoint))
        {
            Assert.Ignore("Live Azure credentials not configured. Skipping.");
            return;
        }

        // 1. Live Policy Indexer: Seed all 8 policies into Azure AI Search
        var indexer = new PolicyIndexer(_configuration, NullLogger<PolicyIndexer>.Instance);
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && (!Directory.Exists(Path.Combine(dir.FullName, "src")) || !File.Exists(Path.Combine(dir.FullName, "LoanAssistant.slnx"))))
        {
            dir = dir.Parent;
        }
        var seedDir = Path.Combine(dir!.FullName, "src", "Loan.Infrastructure", "Search", "SeedPolicies");
        
        await indexer.SynchronizeIndexAndSeedAsync(seedDir);
        await Task.Delay(2000); // Allow replica convergence

        // 2. Live Document Extraction with GPT-4o
        var chatModel = new AzureOpenAIChatModel(_configuration);
        var fallbackExtractor = new SyntheticDocumentExtractor();
        var llmExtractor = new AzureOpenAiDocumentExtractor(chatModel, fallbackExtractor, NullLogger<AzureOpenAiDocumentExtractor>.Instance);

        var sampleDocPath = Path.Combine(dir.FullName, "src", "Loan.Web", "SampleDocuments", "Alice_Cooper_Paystub_Verified.txt");
        using var fileStream = File.OpenRead(sampleDocPath);
        var extractResult = await llmExtractor.ExtractFieldsAsync(fileStream, "Alice_Cooper_Paystub_Verified.txt", "text/plain");

        Assert.That(extractResult.Success, Is.True);
        Assert.That(extractResult.ExtractedFields.Any(f => f.FieldName == "MonthlyGrossIncome" || f.FieldName == "GrossIncome" || f.FieldName == "StatedIncome"), Is.True);
        var incomeField = extractResult.ExtractedFields.First(f => f.FieldName == "MonthlyGrossIncome" || f.FieldName == "GrossIncome" || f.FieldName == "StatedIncome");
        Assert.That(incomeField.ConfidenceScore, Is.GreaterThanOrEqualTo(0.85f));

        // 3. Live 3 Specialist Agents + Orchestrator Execution
        var policyRetriever = new AzureAiSearchPolicyRetriever(_configuration, NullLogger<AzureAiSearchPolicyRetriever>.Instance);
        var docAgent = new DocumentAnalysisAgent(chatModel);
        var eligAgent = new EligibilityAnalysisAgent(chatModel);
        var compAgent = new ComplianceReviewAgent(policyRetriever, chatModel);

        // Build domain application
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-LIVE-001", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(2500m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetIncomeVerified(true, new Money(12000m), DateTime.UtcNow);
        facts.SetCreditVerified(true, 750, DateTime.UtcNow);

        var app = new LoanApplication("APP-LIVE-001", "APP-100", rules, facts, DateTime.UtcNow);
        
        // Add extracted document
        var docRecord = new ExtractedDocumentRecord(
            documentId: extractResult.DocumentId,
            applicationId: "APP-LIVE-001",
            fileName: "Alice_Cooper_Paystub_Verified.txt",
            contentType: "text/plain",
            fileSizeBytes: 1024,
            storageReference: "DOC-BLOB-LIVE-TEST",
            hashSha256: "abc123hash",
            documentType: Domain.Documents.DocumentType.Paystub,
            uploadedAtUtc: DateTime.UtcNow);

        var domainFields = extractResult.ExtractedFields.Select(f => new Domain.Documents.ExtractedFieldRecord(
            fieldName: f.FieldName,
            displayValue: f.RawValue ?? "",
            rawValueMasked: f.RawValue ?? "",
            confidenceScore: f.ConfidenceScore,
            sourceDocumentId: f.SourceDocumentId,
            provenanceExcerpt: "Live GPT-4o Extraction",
            isSensitive: false,
            isValidFormat: true,
            status: Domain.Documents.FieldConfirmationStatus.ConfirmedByApplicant)).ToList();

        docRecord.AddExtractedFields(domainFields);
        app.AddDocumentRecord(docRecord, DateTime.UtcNow);

        // Add 60-day bank statement so mortgage requirements are satisfied
        var bankRecord = new ExtractedDocumentRecord(
            documentId: "DOC-BANK-01",
            applicationId: "APP-LIVE-001",
            fileName: "Alice_Cooper_BankStatement_60Day.txt",
            contentType: "text/plain",
            fileSizeBytes: 2048,
            storageReference: "DOC-BLOB-BANK",
            hashSha256: "def456hash",
            documentType: Domain.Documents.DocumentType.BankStatement,
            uploadedAtUtc: DateTime.UtcNow);
        app.AddDocumentRecord(bankRecord, DateTime.UtcNow);

        // Deterministic eligibility evaluation
        app.EvaluateEligibility(DateTime.UtcNow);

        // Run Multi-Agent Orchestrator
        var dummyAppRepo = new DummyRepo(app);
        var dummyRecRepo = new DummyRecRepo();
        var draftHandler = new SaveRecommendationDraftCommandHandler(dummyAppRepo, dummyRecRepo);
        var orchestrator = new RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, draftHandler, chatModel);

        var recDto = await orchestrator.ProcessApplicationAsync(app);

        Assert.That(recDto, Is.Not.Null);
        Assert.That(recDto.DecisionRecommendation, Is.EqualTo(RecommendationType.Approve));
        Assert.That(recDto.SummaryReasoning, Is.Not.Null.And.Not.Empty);
        Assert.That(recDto.SummaryReasoning, Does.Not.Contain("Degraded Service"));
        Assert.That(recDto.Citations, Is.Not.Null);
    }

    private class DummyRepo : ILoanApplicationRepository
    {
        private readonly LoanApplication _app;
        public DummyRepo(LoanApplication app) => _app = app;
        public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<LoanApplication>>([_app]);
        public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default) => Task.FromResult<LoanApplication?>(_app);
        public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<LoanApplication>>([_app]);
        public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class DummyRecRepo : IRecommendationRepository
    {
        public Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default) => Task.FromResult<Recommendation?>(null);
        public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
