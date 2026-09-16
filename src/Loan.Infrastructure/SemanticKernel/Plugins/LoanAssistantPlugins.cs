using System.ComponentModel;
using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Recommendations;
using Microsoft.SemanticKernel;

namespace Loan.Infrastructure.SemanticKernel.Plugins;

public class IdentityPlugin
{
    private readonly IIdentityReader _identityReader;

    public IdentityPlugin(IIdentityReader identityReader)
    {
        _identityReader = identityReader;
    }

    [KernelFunction, Description("Retrieve typed synthetic identity verification records for a loan application.")]
    public async Task<string> GetIdentityStatusAsync(
        [Description("The unique loan application identifier e.g. APP-2026-001")] string applicationId)
    {
        var record = await _identityReader.VerifyIdentityAsync(applicationId);
        return JsonSerializer.Serialize(record);
    }
}

public class CreditPlugin
{
    private readonly ICreditReader _creditReader;

    public CreditPlugin(ICreditReader creditReader)
    {
        _creditReader = creditReader;
    }

    [KernelFunction, Description("Retrieve authoritative credit report data and verified credit score for an applicant.")]
    public async Task<string> GetCreditStatusAsync(
        [Description("The unique loan application identifier e.g. APP-2026-001")] string applicationId)
    {
        var record = await _creditReader.GetCreditScoreAsync(applicationId);
        return JsonSerializer.Serialize(record);
    }
}

public class PolicySearchPlugin
{
    private readonly IPolicyRetriever _policyRetriever;

    public PolicySearchPlugin(IPolicyRetriever policyRetriever)
    {
        _policyRetriever = policyRetriever;
    }

    [KernelFunction, Description("Search authoritative underwriting policy guidelines and retrieved citations using hybrid RAG.")]
    public async Task<string> SearchPolicyAsync(
        [Description("Search query regarding lending policy, DTI caps, or requirements")] string query,
        [Description("Target product identifier, e.g. LOAN-PERSONAL or MORTGAGE-STD")] string? targetProductId = null,
        [Description("Effective policy version e.g. v2.0 or v1.2")] string? policyVersion = null)
    {
        var results = await _policyRetriever.SearchPolicyAsync(query, targetProductId, policyVersion);
        return JsonSerializer.Serialize(results);
    }
}

public class DraftSaverPlugin
{
    private readonly SaveRecommendationDraftCommandHandler _saveDraftHandler;

    public DraftSaverPlugin(SaveRecommendationDraftCommandHandler saveDraftHandler)
    {
        _saveDraftHandler = saveDraftHandler;
    }

    [KernelFunction, Description("Save a synthesized recommendation draft to the repository.")]
    public async Task<string> SaveRecommendationDraftAsync(
        [Description("The target loan application identifier e.g. APP-2026-001")] string applicationId,
        [Description("Deterministic routing state: PendingInformation, ReadyForOfficerReview, or ManualReview")] string routingState,
        [Description("Calculated risk score between 0.0 and 1.0")] double riskScore,
        [Description("LLM synthesized reasoning summary explaining the recommendation")] string summaryReasoning)
    {
        var command = new SaveRecommendationDraftCommand(
            ApplicationId: applicationId,
            SummaryReasoning: summaryReasoning,
            RoutingState: routingState,
            RiskScore: riskScore);

        var saved = await _saveDraftHandler.HandleAsync(command);
        return JsonSerializer.Serialize(saved);
    }
}
