using Loan.Domain.Applications;
using Loan.Domain.Products;

namespace Loan.Domain.Eligibility;

/// <summary>
/// Domain service providing deterministic calculations for loan eligibility and risk factors.
/// Per BR-03: DTI and eligibility indicators MUST be calculated by deterministic domain rules, never LLM arithmetic.
/// </summary>
public static class EligibilityCalculator
{
    public static EligibilityIndicators Evaluate(ApplicantFacts facts, ProductRules rules, DateTime evaluationTimestampUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(rules);

        var income = facts.EffectiveMonthlyIncome.Amount;
        var debts = facts.MonthlyDebts.Amount;
        var loanAmount = facts.RequestedLoanAmount.Amount;
        var propertyValue = facts.EstimatedPropertyValue.Amount;

        // DTI Calculation (Debts / Income)
        decimal dti = income > 0 ? debts / income : 1.0m;

        // LTV Calculation (Loan / Property Value)
        decimal ltv = propertyValue > 0 ? loanAmount / propertyValue : 1.0m;

        // Rule Checks
        bool isDtiEligible = dti <= rules.MaxDtiRatio;
        bool isLtvEligible = ltv <= rules.MaxLtvRatio;
        bool isCreditEligible = facts.EffectiveCreditScore >= rules.MinCreditScore;
        bool isIncomeThresholdEligible = facts.EffectiveMonthlyIncome >= rules.MinMonthlyIncome;
        bool isLoanAmountEligible = facts.RequestedLoanAmount <= rules.MaxLoanAmount;

        var unmetConditions = new List<string>();

        if (!isDtiEligible)
            unmetConditions.Add($"DTI ratio ({dti:P1}) exceeds maximum allowed threshold ({rules.MaxDtiRatio:P1}).");
        
        if (!isLtvEligible)
            unmetConditions.Add($"LTV ratio ({ltv:P1}) exceeds maximum allowed threshold ({rules.MaxLtvRatio:P1}).");

        if (!isCreditEligible)
            unmetConditions.Add($"Credit score ({facts.EffectiveCreditScore}) is below minimum requirement ({rules.MinCreditScore}).");

        if (!isIncomeThresholdEligible)
            unmetConditions.Add($"Monthly income ({facts.EffectiveMonthlyIncome}) is below product minimum ({rules.MinMonthlyIncome}).");

        if (!isLoanAmountEligible)
            unmetConditions.Add($"Requested loan amount ({facts.RequestedLoanAmount}) exceeds maximum limit ({rules.MaxLoanAmount}).");

        if (rules.RequiresIdentityVerification && !facts.IsIdentityVerified)
            unmetConditions.Add("Applicant identity verification is pending or failed.");

        if (rules.RequiresIncomeVerification && !facts.IsIncomeVerified)
            unmetConditions.Add("Applicant income verification is pending or unconfirmed.");

        // Overall Status Determination
        EligibilityStatus status;
        if (unmetConditions.Count == 0)
        {
            status = EligibilityStatus.Eligible;
        }
        else if (!isDtiEligible || !isLtvEligible || !isCreditEligible)
        {
            status = EligibilityStatus.ReferToHuman;
        }
        else
        {
            status = EligibilityStatus.Ineligible;
        }

        return new EligibilityIndicators(
            debtToIncomeRatio: dti,
            loanToValueRatio: ltv,
            isDtiEligible: isDtiEligible,
            isLtvEligible: isLtvEligible,
            isCreditScoreEligible: isCreditEligible,
            isIncomeThresholdEligible: isIncomeThresholdEligible,
            isLoanAmountEligible: isLoanAmountEligible,
            isIdentityVerified: facts.IsIdentityVerified,
            isIncomeVerified: facts.IsIncomeVerified,
            isCreditVerified: facts.IsCreditVerified,
            status: status,
            unmetConditions: unmetConditions,
            evaluatedAtUtc: evaluationTimestampUtc
        );
    }
}
