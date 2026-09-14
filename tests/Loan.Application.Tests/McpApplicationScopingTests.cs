using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class McpApplicationScopingTests
{
    private class InMemoryAppRepository : ILoanApplicationRepository
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

    private class InMemoryRecRepository : IRecommendationRepository
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

    private McpToolServer _mcpServer = null!;
    private InMemoryAppRepository _appRepo = null!;

    [SetUp]
    public async Task SetUp()
    {
        _appRepo = new InMemoryAppRepository();
        var recRepo = new InMemoryRecRepository();

        var rules = ProductRules.CreateStandardMortgage("v1.0");
        var facts1 = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        var app1 = new LoanApplication("APP-2026-001", "APP-100", rules, facts1, DateTime.UtcNow);

        var facts2 = new ApplicantFacts("APP-200", "John Doe", "SYN-123456", new Money(10000m), new Money(2000m), new Money(250000m), new Money(400000m), 720, "Employed", "Refinance");
        var app2 = new LoanApplication("APP-2026-002", "APP-200", rules, facts2, DateTime.UtcNow);

        await _appRepo.AddAsync(app1);
        await _appRepo.AddAsync(app2);

        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(_appRepo, recRepo);

        _mcpServer = new McpToolServer(
            _appRepo,
            new SyntheticIdentityService(),
            new SyntheticIncomeService(),
            new SyntheticCreditService(),
            new SyntheticPolicyRetriever(),
            saveDraftHandler);
    }

    [Test]
    public async Task GetIdentityStatus_ValidMatchingApplicationAndSyntheticId_Succeeds()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "get_identity_status",
            arguments = new
            {
                applicationId = "APP-2026-001",
                syntheticId = "SYN-888777"
            }
        }, 1);

        var response = await _mcpServer.HandleRequestAsync(request);

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.False);
        Assert.That(result.Content[0].Text, Does.Contain("Alice Cooper"));
    }

    [Test]
    public async Task GetIdentityStatus_CrossApplicationSyntheticIdMismatch_ReturnsDeniedError()
    {
        // APP-2026-001 is bound to SYN-888777, but request asks for SYN-123456 (belonging to APP-2026-002)
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "get_identity_status",
            arguments = new
            {
                applicationId = "APP-2026-001",
                syntheticId = "SYN-123456"
            }
        }, 2);

        var response = await _mcpServer.HandleRequestAsync(request);

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("CrossApplicationMismatch"));
        Assert.That(result.Content[0].Text, Does.Contain("Cross-application access denied"));
    }

    [Test]
    public async Task GetIncomeVerification_CrossApplicationSyntheticIdMismatch_ReturnsDeniedError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "get_income_verification",
            arguments = new
            {
                applicationId = "APP-2026-002",
                syntheticId = "SYN-888777"
            }
        }, 3);

        var response = await _mcpServer.HandleRequestAsync(request);

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("CrossApplicationMismatch"));
    }

    [Test]
    public async Task GetCreditScore_NonExistentApplication_ReturnsUnknownApplicationError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "get_credit_score",
            arguments = new
            {
                applicationId = "APP-NONEXISTENT",
                syntheticId = "SYN-888777"
            }
        }, 4);

        var response = await _mcpServer.HandleRequestAsync(request);

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("UnknownApplication"));
    }
}
