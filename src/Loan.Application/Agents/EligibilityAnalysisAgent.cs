using Loan.Application.Abstractions;
using Loan.Domain.Applications;
using Loan.Domain.Eligibility;

namespace Loan.Application.Agents;

public class EligibilityAnalysisAgent
{
    private readonly IChatModel _chatModel;

    public EligibilityAnalysisAgent(IChatModel chatModel)
    {
        _chatModel = chatModel;
    }

    public async Task<EligibilityAnalysisResult> AnalyzeAsync(
        LoanApplication application,
        EligibilityIndicators indicators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(indicators);

        var facts = application.Facts;

        var contextPrompt = $"Application ID: {application.ApplicationId}\n" +
                            $"Product: {application.ProductRules.ProductName} ({application.ProductRules.ProductId})\n" +
                            $"Effective Income: ${facts.EffectiveMonthlyIncome.Amount:N2} (Verified: {facts.IsIncomeVerified})\n" +
                            $"Effective Credit Score: {facts.EffectiveCreditScore} (Verified: {facts.IsCreditVerified})\n" +
                            $"DTI Ratio: {(indicators.DebtToIncomeRatio.HasValue ? indicators.DebtToIncomeRatio.Value.ToString("P1") : "N/A")} (Max Allowed: {application.ProductRules.MaxDtiRatio:P1})\n" +
                            $"LTV Ratio: {(indicators.LoanToValueRatio.HasValue ? indicators.LoanToValueRatio.Value.ToString("P1") : "N/A")} (Max Allowed: {application.ProductRules.MaxLtvRatio:P1})\n" +
                            $"Authoritative Deterministic Status: {indicators.Status}\n" +
                            $"Unmet Conditions: {(indicators.UnmetConditions.Count > 0 ? string.Join("; ", indicators.UnmetConditions) : "None")}";

        var messages = new List<ChatMessage>
        {
            new("system", AgentPrompts.EligibilityAgentSystemPrompt),
            new("user", $"Explain the financial factors for this application:\n{contextPrompt}")
        };

        var notes = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken: cancellationToken);

        return new EligibilityAnalysisResult(
            ApplicationId: application.ApplicationId,
            DebtToIncomeRatio: indicators.DebtToIncomeRatio,
            LoanToValueRatio: indicators.LoanToValueRatio,
            EffectiveCreditScore: facts.EffectiveCreditScore,
            EffectiveMonthlyIncome: facts.EffectiveMonthlyIncome.Amount,
            IsDtiEligible: indicators.IsDtiEligible,
            IsLtvEligible: indicators.IsLtvEligible,
            IsCreditScoreEligible: indicators.IsCreditScoreEligible,
            DeterministicStatus: indicators.Status.ToString(),
            FinancialRiskFactors: indicators.UnmetConditions.ToList(),
            SummaryNotes: notes);
    }
}
