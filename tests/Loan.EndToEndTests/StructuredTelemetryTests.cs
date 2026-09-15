using Loan.Application.Abstractions;
using Loan.Application.Agents;
using Loan.Application.Common;
using Loan.Application.DTOs;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Persistence;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Telemetry;
using Loan.Infrastructure.Verification;
using Loan.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Loan.EndToEndTests;

[TestFixture]
public class StructuredTelemetryTests
{
    private InMemoryTelemetryCollector _telemetryCollector = null!;

    [SetUp]
    public void SetUp()
    {
        _telemetryCollector = new InMemoryTelemetryCollector();
    }

    [Test]
    public void CorrelationContext_EnsureCorrelationId_ShouldGenerateAndPropagateId()
    {
        var id1 = CorrelationContext.EnsureCorrelationId();
        Assert.That(id1, Is.Not.Null.And.Not.Empty);

        var id2 = CorrelationContext.Current;
        Assert.That(id2, Is.EqualTo(id1));
    }

    [Test]
    public void RecordTokensAndLatencies_ShouldAccumulateCorrectlyInRequestTelemetry()
    {
        _telemetryCollector.SetCorrelationId("TEST-CORR-100");

        _telemetryCollector.RecordTokens(150, 75);
        _telemetryCollector.RecordTokens(50, 25);
        _telemetryCollector.RecordLlmLatency(120.5);
        _telemetryCollector.RecordRagLatency(45.2, 3);
        _telemetryCollector.RecordAgentStageLatency("DocumentAnalysis", 30.1);
        _telemetryCollector.RecordAgentStageLatency("EligibilityAnalysis", 20.4);
        _telemetryCollector.RecordToolCall();
        _telemetryCollector.RecordToolCall();
        _telemetryCollector.RecordRoutingDistribution("ReadyForOfficerReview");

        var telemetry = _telemetryCollector.GetCurrentTelemetry();

        Assert.That(telemetry.CorrelationId, Is.EqualTo("TEST-CORR-100"));
        Assert.That(telemetry.PromptTokens, Is.EqualTo(200));
        Assert.That(telemetry.CompletionTokens, Is.EqualTo(100));
        Assert.That(telemetry.TotalTokens, Is.EqualTo(300));
        Assert.That(telemetry.LlmLatencyMs, Is.EqualTo(120.5));
        Assert.That(telemetry.RagLatencyMs, Is.EqualTo(45.2));
        Assert.That(telemetry.RetrievalHitCount, Is.EqualTo(3));
        Assert.That(telemetry.ToolCallCount, Is.EqualTo(2));
        Assert.That(telemetry.AgentStageLatencyMs["DocumentAnalysis"], Is.EqualTo(30.1));
        Assert.That(telemetry.AgentStageLatencyMs["EligibilityAnalysis"], Is.EqualTo(20.4));
        Assert.That(telemetry.RoutingDistribution, Is.EqualTo("ReadyForOfficerReview"));
    }

    [Test]
    public async Task QueryHandler_WithTelemetry_ShouldRecordPromptTokensAndRagMetrics()
    {
        _telemetryCollector.SetCorrelationId("QUERY-TEST-200");
        var policyRetriever = new SyntheticPolicyRetriever(_telemetryCollector);
        var chatModel = new SyntheticChatModel(_telemetryCollector);
        var handler = new AskProductQuestionQueryHandler(policyRetriever, chatModel);

        var response = await handler.HandleAsync(new AskProductQuestionQuery("What is the maximum LTV ratio?"));

        Assert.That(response.Answer, Does.Contain("80.0%"));

        var telemetry = _telemetryCollector.GetCurrentTelemetry();
        Assert.That(telemetry.PromptTokens, Is.GreaterThan(0));
        Assert.That(telemetry.CompletionTokens, Is.GreaterThan(0));
        Assert.That(telemetry.TotalTokens, Is.GreaterThan(0));
        Assert.That(telemetry.RagLatencyMs, Is.GreaterThanOrEqualTo(0));
        Assert.That(telemetry.RetrievalHitCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task MultiAgentOrchestration_WithTelemetry_ShouldRecordAgentStageLatencies()
    {
        _telemetryCollector.SetCorrelationId("AGENT-TEST-300");

        var appRepo = new InMemoryLoanApplicationRepository();
        var recRepo = new InMemoryRecommendationRepository();
        var chatModel = new SyntheticChatModel(_telemetryCollector);
        var policyRetriever = new SyntheticPolicyRetriever(_telemetryCollector);
        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(appRepo, recRepo);

        var docAgent = new DocumentAnalysisAgent(chatModel);
        var eligAgent = new EligibilityAnalysisAgent(chatModel);
        var compAgent = new ComplianceReviewAgent(policyRetriever, chatModel);
        var orchestrator = new RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, saveDraftHandler, chatModel, _telemetryCollector);

        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-TEL-1", "Telemetry User", "SYN-999000", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Full-Time", "Purchase");
        var app = new LoanApplication("APP-2026-TEL", "APP-TEL-1", rules, facts, DateTime.UtcNow);
        app.Submit(DateTime.UtcNow);
        app.EvaluateEligibility(DateTime.UtcNow);
        await appRepo.AddAsync(app);

        var rec = await orchestrator.ProcessApplicationAsync(app);

        Assert.That(rec, Is.Not.Null);

        var telemetry = _telemetryCollector.GetCurrentTelemetry();
        Assert.That(telemetry.AgentStageLatencyMs.ContainsKey("DocumentAnalysis"), Is.True);
        Assert.That(telemetry.AgentStageLatencyMs.ContainsKey("EligibilityAnalysis"), Is.True);
        Assert.That(telemetry.AgentStageLatencyMs.ContainsKey("ComplianceReview"), Is.True);
        Assert.That(telemetry.AgentStageLatencyMs.ContainsKey("OrchestratorSynthesis"), Is.True);
        Assert.That(telemetry.RoutingDistribution, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task HealthController_GetTelemetry_ShouldReturnStructuredMetrics()
    {
        _telemetryCollector.SetCorrelationId("HEALTH-TEL-400");
        _telemetryCollector.RecordTokens(40, 20);

        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantTestDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;", b => b.MigrationsAssembly("Loan.Infrastructure"))
            .Options;
        using var dbContext = new LoanDbContext(options);
        var config = new ConfigurationBuilder().Build();
        var chatModel = new SyntheticChatModel(_telemetryCollector);
        var policyRetriever = new SyntheticPolicyRetriever(_telemetryCollector);

        var controller = new HealthController(dbContext, chatModel, policyRetriever, config, _telemetryCollector);

        var result = controller.GetTelemetry(10) as OkObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));

        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
        Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("StructuredTelemetryMetrics"));
        Assert.That(root.GetProperty("count").GetInt32(), Is.GreaterThan(0));
    }
}
