using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Eligibility;

namespace Loan.Application.Agents;

public class RecommendationOrchestratorAgent
{
    private readonly DocumentAnalysisAgent _documentAgent;
    private readonly EligibilityAnalysisAgent _eligibilityAgent;
    private readonly ComplianceReviewAgent _complianceAgent;
    private readonly SaveRecommendationDraftCommandHandler _saveDraftHandler;
    private readonly IChatModel _chatModel;
    private readonly ITelemetryCollector? _telemetryCollector;

    public const string NonApprovalDisclaimer = "Disclaimer: Informational recommendation draft prepared by specialist agents for Loan Officer review only. Does NOT constitute a loan commitment, rate lock, or approval decision.";

    public RecommendationOrchestratorAgent(
        DocumentAnalysisAgent documentAgent,
        EligibilityAnalysisAgent eligibilityAgent,
        ComplianceReviewAgent complianceAgent,
        SaveRecommendationDraftCommandHandler saveDraftHandler,
        IChatModel chatModel,
        ITelemetryCollector? telemetryCollector = null)
    {
        _documentAgent = documentAgent;
        _eligibilityAgent = eligibilityAgent;
        _complianceAgent = complianceAgent;
        _saveDraftHandler = saveDraftHandler;
        _chatModel = chatModel;
        _telemetryCollector = telemetryCollector;
    }

    public async Task<RecommendationDto> ProcessApplicationAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (application.Indicators == null)
        {
            throw new InvalidOperationException("Eligibility must be evaluated before multi-agent orchestration.");
        }

        // 1. Execute Specialist Agents with stage latency timing
        var docSw = System.Diagnostics.Stopwatch.StartNew();
        var docResult = await _documentAgent.AnalyzeAsync(application, cancellationToken);
        docSw.Stop();
        _telemetryCollector?.RecordAgentStageLatency("DocumentAnalysis", docSw.Elapsed.TotalMilliseconds);

        var eligSw = System.Diagnostics.Stopwatch.StartNew();
        var eligResult = await _eligibilityAgent.AnalyzeAsync(application, application.Indicators, cancellationToken);
        eligSw.Stop();
        _telemetryCollector?.RecordAgentStageLatency("EligibilityAnalysis", eligSw.Elapsed.TotalMilliseconds);

        var compSw = System.Diagnostics.Stopwatch.StartNew();
        var compResult = await _complianceAgent.ReviewAsync(application, application.Indicators, cancellationToken);
        compSw.Stop();
        _telemetryCollector?.RecordAgentStageLatency("ComplianceReview", compSw.Elapsed.TotalMilliseconds);

        // 2. AUTHORITATIVE DETERMINISTIC OVERRIDES
        // RoutingState and RiskScore are derived 100% deterministically from Domain EligibilityIndicators
        var deterministicStatus = application.Indicators.Status;

        string routingState = deterministicStatus switch
        {
            EligibilityStatus.PendingInformation => "PendingInformation",
            EligibilityStatus.Ineligible => "ManualReview",
            EligibilityStatus.ReferToHuman => "ManualReview",
            EligibilityStatus.Eligible => "ReadyForOfficerReview",
            _ => "ManualReview"
        };

        double riskScore = deterministicStatus switch
        {
            EligibilityStatus.Eligible => 0.15,
            EligibilityStatus.PendingInformation => 0.50,
            EligibilityStatus.ReferToHuman => 0.65,
            EligibilityStatus.Ineligible => 0.85,
            _ => 0.65
        };

        // 3. Aggregate Unresolved Items & Policy Exceptions
        var unresolvedItems = new List<string>(docResult.UnresolvedFields);
        foreach (var cond in application.Indicators.UnmetConditions)
        {
            if (cond.Contains("verification", StringComparison.OrdinalIgnoreCase) && !unresolvedItems.Contains(cond))
            {
                unresolvedItems.Add(cond);
            }
        }

        var policyExceptions = new List<string>(compResult.PolicyViolations);
        foreach (var cond in application.Indicators.UnmetConditions)
        {
            if (!cond.Contains("verification", StringComparison.OrdinalIgnoreCase) && !policyExceptions.Contains(cond))
            {
                policyExceptions.Add(cond);
            }
        }

        // 4. Generate LLM Summary Reasoning with stage timing
        var contextPrompt = $"Application ID: {application.ApplicationId}\n" +
                            $"Deterministic Status: {deterministicStatus}\n" +
                            $"Routing State: {routingState}\n" +
                            $"Document Notes: {docResult.AnalysisNotes}\n" +
                            $"Eligibility Notes: {eligResult.SummaryNotes}\n" +
                            $"Compliance Notes: {compResult.ComplianceNotes}";

        var messages = new List<ChatMessage>
        {
            new("system", AgentPrompts.OrchestratorSystemPrompt),
            new("user", $"Synthesize recommendation summary:\n{contextPrompt}")
        };

        var orchSw = System.Diagnostics.Stopwatch.StartNew();
        var summaryReasoning = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken: cancellationToken);
        orchSw.Stop();

        _telemetryCollector?.RecordAgentStageLatency("OrchestratorSynthesis", orchSw.Elapsed.TotalMilliseconds);
        _telemetryCollector?.RecordRoutingDistribution(routingState);

        // 5. Construct Schema-Valid Draft
        var draft = new RecommendationDraft(
            ApplicationId: application.ApplicationId,
            RoutingState: routingState,
            RiskScore: riskScore,
            SummaryReasoning: summaryReasoning,
            UnresolvedItems: unresolvedItems,
            PolicyExceptions: policyExceptions,
            Citations: compResult.Citations,
            NonApprovalDisclaimer: NonApprovalDisclaimer);

        // 6. Safe Draft Persistence via SaveRecommendationDraftCommandHandler
        var command = new SaveRecommendationDraftCommand(
            ApplicationId: draft.ApplicationId,
            SummaryReasoning: draft.SummaryReasoning,
            RoutingState: draft.RoutingState,
            UnresolvedItems: draft.UnresolvedItems,
            PolicyExceptions: draft.PolicyExceptions,
            Citations: draft.Citations,
            RiskScore: draft.RiskScore,
            ActorId: "MultiAgentOrchestrator",
            ActorRole: "SystemWorker");

        return await _saveDraftHandler.HandleAsync(command, cancellationToken);
    }
}
