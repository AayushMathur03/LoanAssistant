using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Eligibility;

namespace Loan.Application.Agents;

public class ComplianceReviewAgent
{
    private readonly IPolicyRetriever _policyRetriever;
    private readonly IChatModel _chatModel;

    public ComplianceReviewAgent(IPolicyRetriever policyRetriever, IChatModel chatModel)
    {
        _policyRetriever = policyRetriever;
        _chatModel = chatModel;
    }

    public async Task<ComplianceReviewResult> ReviewAsync(
        LoanApplication application,
        EligibilityIndicators indicators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        var query = $"Underwriting guidelines for {application.ProductRules.ProductName} DTI max {application.ProductRules.MaxDtiRatio:P0} credit min {application.ProductRules.MinCreditScore}";
        var searchResults = (await _policyRetriever.SearchPolicyAsync(
            query: query,
            targetProductId: application.ProductId,
            effectiveVersion: application.ProductRules.EffectiveVersion,
            topK: 3,
            cancellationToken: cancellationToken)).ToList();

        var citations = searchResults
            .Select(r => new CitationInput(r.Title, r.Version, r.Section, r.Content.Length > 150 ? r.Content[..150] + "..." : r.Content))
            .ToList();

        if (!citations.Any())
        {
            citations.Add(new CitationInput("Standard Underwriting Policy Guide", application.ProductRules.EffectiveVersion, "Section 1.0", "Standard underwriting guidelines apply."));
        }

        var policyViolations = (indicators?.UnmetConditions ?? Array.Empty<string>())
            .Where(c => !c.Contains("verification", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var contextPrompt = $"Product: {application.ProductRules.ProductName} (Ver: {application.ProductRules.EffectiveVersion})\n" +
                            $"Policy Violations: {(policyViolations.Count > 0 ? string.Join("; ", policyViolations) : "None")}\n" +
                            $"Retrieved Guidelines:\n" + string.Join("\n", searchResults.Select(r => $"[{r.Title} Section {r.Section}] {r.Content}"));

        var messages = new List<ChatMessage>
        {
            new("system", AgentPrompts.ComplianceAgentSystemPrompt),
            new("user", $"Evaluate compliance and generate policy notes:\n{contextPrompt}")
        };

        var notes = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken: cancellationToken);

        return new ComplianceReviewResult(
            ApplicationId: application.ApplicationId,
            EffectivePolicyVersion: searchResults.FirstOrDefault()?.Version ?? application.ProductRules.EffectiveVersion,
            PolicyViolations: policyViolations,
            Citations: citations,
            HasSufficientEvidence: searchResults.Any(),
            ComplianceNotes: notes);
    }
}
