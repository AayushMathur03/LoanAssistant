using Loan.Application.Abstractions;
using Loan.Application.Documents;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Products;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class CrossApplicationIsolationTests
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
        private readonly Dictionary<string, Domain.Recommendations.Recommendation> _recs = new();

        public Task<Domain.Recommendations.Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default)
        {
            _recs.TryGetValue(recommendationId, out var r);
            return Task.FromResult(r);
        }

        public Task SaveAsync(Domain.Recommendations.Recommendation recommendation, CancellationToken cancellationToken = default)
        {
            _recs[recommendation.RecommendationId] = recommendation;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task McpServer_CrossApplicationSyntheticIdMismatch_ReturnsError()
    {
        var appRepo = new InMemoryAppRepo();
        var recRepo = new InMemoryRecRepo();
        var identityService = new SyntheticIdentityService();
        var incomeService = new SyntheticIncomeService();
        var creditService = new SyntheticCreditService();
        var policyRetriever = new SyntheticPolicyRetriever();
        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(appRepo, recRepo);

        var mcpServer = new McpToolServer(appRepo, identityService, incomeService, creditService, policyRetriever, saveDraftHandler);

        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        var app = new LoanApplication("APP-TENANT-A", "APP-100", rules, facts, DateTime.UtcNow);
        await appRepo.AddAsync(app);

        // Attempting to query Tenant A's application with Tenant B's synthetic ID
        var args = new { name = "get_income_verification", arguments = new { applicationId = "APP-TENANT-A", syntheticId = "SYN-300400" } };
        var jsonRpcReq = new JsonRpcRequest("2.0", "tools/call", args, 1);

        var response = await mcpServer.HandleRequestAsync(jsonRpcReq, "Actor-1", "SystemWorker");

        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("CrossApplicationMismatch"));
    }

    [Test]
    public void DocumentOverride_CrossApplicationDocumentId_ThrowsInvalidOperationException()
    {
        var appRepo = new InMemoryAppRepo();
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        var appA = new LoanApplication("APP-TENANT-A", "APP-100", rules, facts, DateTime.UtcNow);
        var appB = new LoanApplication("APP-TENANT-B", "APP-200", rules, facts, DateTime.UtcNow);

        var timestamp = DateTime.UtcNow;
        var docRecord = new ExtractedDocumentRecord("DOC-B-001", "APP-TENANT-B", "paystub.pdf", "application/pdf", 1024, "App_Data/Uploads/paystub.pdf", "hash", DocumentType.Paystub, timestamp);
        docRecord.AddExtractedFields(new[] { new ExtractedFieldRecord("MonthlyGrossIncome", "10000", "10000", 0.95f, "DOC-B-001", "Line 1", false, true, FieldConfirmationStatus.Unconfirmed) });
        appB.AddDocumentRecord(docRecord, timestamp);

        appRepo.AddAsync(appA).Wait();
        appRepo.AddAsync(appB).Wait();

        var handler = new ConfirmOrOverrideExtractedFieldsCommandHandler(appRepo);

        // Attempting to override Document B belonging to Tenant B on Application A
        var items = new List<FieldConfirmationItem> { new("DOC-B-001", "MonthlyGrossIncome", "12000", "Wrong app attempt") };
        var command = new ConfirmOrOverrideExtractedFieldsCommand("APP-TENANT-A", "OFFICER-1", "Officer", items);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.HandleAsync(command));
    }
}
