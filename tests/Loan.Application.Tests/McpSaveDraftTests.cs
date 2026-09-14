using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class McpSaveDraftTests
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

    private McpToolServer _mcpServer = null!;
    private InMemoryAppRepository _appRepo = null!;
    private InMemoryRecRepository _recRepo = null!;

    [SetUp]
    public async Task SetUp()
    {
        _appRepo = new InMemoryAppRepository();
        _recRepo = new InMemoryRecRepository();

        var rules = ProductRules.CreateStandardMortgage("v1.0");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Employed", "Purchase");
        var app = new LoanApplication("APP-2026-001", "APP-100", rules, facts, DateTime.UtcNow);

        await _appRepo.AddAsync(app);

        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);

        _mcpServer = new McpToolServer(
            _appRepo,
            new SyntheticIdentityService(),
            new SyntheticIncomeService(),
            new SyntheticCreditService(),
            new SyntheticPolicyRetriever(),
            saveDraftHandler);
    }

    [Test]
    public async Task SaveDraft_ValidRoutingState_CreatesDraftRecommendation()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "Applicant meets DTI and LTV limits based on verified income.",
                routingState = "ReadyForOfficerReview",
                policyExceptions = new[] { "Minor credit inquiry warning" },
                citations = new[]
                {
                    new { documentTitle = "Mortgage Policy Guide", policyVersion = "v2.0", sectionOrPage = "Section 3.1", excerpt = "DTI cap is 43%" }
                }
            }
        }, 1);

        var response = await _mcpServer.HandleRequestAsync(request, headerActorId: "OrchestratorAgent", headerActorRole: "SystemWorker");

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.False);

        var updatedApp = await _appRepo.GetByIdAsync("APP-2026-001");
        Assert.That(updatedApp, Is.Not.Null);
        Assert.That(updatedApp!.CurrentRecommendation, Is.Not.Null);
        Assert.That(updatedApp.CurrentRecommendation!.Status, Is.EqualTo(RecommendationStatus.DraftPreparedBySystem));
        Assert.That(updatedApp.CurrentRecommendation.SummaryReasoning, Does.Contain("Applicant meets DTI"));
        Assert.That(updatedApp.CurrentRecommendation.ApprovedByOfficerId, Is.Null); // Crucial: Officer approval NOT set
    }

    [Test]
    public async Task SaveDraft_AttemptFinalApproveDecision_ReturnsSafetyError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "Attempting to auto-approve",
                routingState = "Approve"
            }
        }, 2);

        var response = await _mcpServer.HandleRequestAsync(request, headerActorId: "MaliciousAgent", headerActorRole: "SystemWorker");

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("Draft recommendations cannot submit final Approve or Reject decisions"));

        var app = await _appRepo.GetByIdAsync("APP-2026-001");
        Assert.That(app!.CurrentRecommendation, Is.Null); // Safety check: No recommendation saved
    }

    [Test]
    public async Task SaveDraft_AttemptFinalRejectDecision_ReturnsSafetyError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "Attempting to auto-reject",
                routingState = "Reject"
            }
        }, 3);

        var response = await _mcpServer.HandleRequestAsync(request, headerActorId: "MaliciousAgent", headerActorRole: "SystemWorker");

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("Draft recommendations cannot submit final Approve or Reject decisions"));
    }

    [Test]
    public async Task SaveDraft_UnauthorizedActorRole_ReturnsUnauthorizedError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "Draft reasoning",
                routingState = "ManualReview"
            }
        }, 4);

        var response = await _mcpServer.HandleRequestAsync(request, headerActorId: "UntrustedExternalActor", headerActorRole: "AnonymousGuest");

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("UnauthorizedAccess"));
    }

    [Test]
    public async Task SaveDraft_ComplianceRole_ReturnsUnauthorizedError()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "Compliance auditor attempting write",
                routingState = "ManualReview"
            }
        }, 5);

        var response = await _mcpServer.HandleRequestAsync(request, headerActorId: "AuditorUser", headerActorRole: "Compliance");

        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.True);
        Assert.That(result.Content[0].Text, Does.Contain("UnauthorizedAccess"));
    }

    [Test]
    public async Task SaveDraft_AuditTrailSemantics_RecordsDraftPreparedAction()
    {
        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "save_draft",
            arguments = new
            {
                applicationId = "APP-2026-001",
                summaryReasoning = "System draft prepared with citations",
                routingState = "ReadyForOfficerReview"
            }
        }, 6);

        await _mcpServer.HandleRequestAsync(request, headerActorId: "OrchestratorAgent", headerActorRole: "SystemWorker");

        var app = await _appRepo.GetByIdAsync("APP-2026-001");
        var rec = app!.CurrentRecommendation!;

        Assert.That(rec.AuditTrail.Count, Is.EqualTo(1));
        Assert.That(rec.AuditTrail[0].Action, Is.EqualTo("DraftPrepared"));
        Assert.That(rec.AuditTrail[0].PerformedBy, Is.EqualTo("OrchestratorAgent"));

        // Officer consequential action produces distinct audit entry
        rec.ApproveByOfficer("OFFICER-44", "Officer approved after reviewing draft.", DateTime.UtcNow);
        Assert.That(rec.AuditTrail.Count, Is.EqualTo(2));
        Assert.That(rec.AuditTrail[1].Action, Is.EqualTo("OfficerApproved"));
        Assert.That(rec.AuditTrail[1].PerformedBy, Is.EqualTo("OFFICER-44"));
    }
}
