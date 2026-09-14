using Loan.Domain.Applications;
using Loan.Domain.Products;

namespace Loan.Domain.Eligibility;

/// <summary>
/// Domain service providing deterministic calculations for loan eligibility and risk factors.
/// Per BR-03: DTI and eligibility indicators MUST be calculated by deterministic domain rules, never LLM arithmetic.
/// Per BR-02: Verified values take precedence over stated facts (verified > confirmed > stated).
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

        var unmetConditions = new List<string>();

        // 1. DTI Calculation & Zero Income Guard
        decimal? dti = null;
        bool isDtiEligible;

        if (income <= 0)
        {
            dti = null;
            isDtiEligible = false;
            unmetConditions.Add("Monthly gross income is zero or invalid ($0.00); DTI calculation cannot be performed.");
        }
        else
        {
            dti = debts / income;
            isDtiEligible = dti.Value <= rules.MaxDtiRatio;
            if (!isDtiEligible)
            {
                unmetConditions.Add($"DTI ratio ({dti.Value:P1}) exceeds maximum allowed threshold ({rules.MaxDtiRatio:P1}).");
            }
        }

        // 2. LTV Calculation & Product Relevance Guard
        decimal? ltv = null;
        bool isLtvEligible;

        if (!rules.RequiresPropertyValuation)
        {
            ltv = null;
            isLtvEligible = true; // Not applicable for unsecured personal loans
        }
        else if (propertyValue <= 0)
        {
            ltv = null;
            isLtvEligible = false;
            unmetConditions.Add("Estimated property value is zero or invalid for mortgage loan; LTV ratio cannot be calculated.");
        }
        else
        {
            ltv = loanAmount / propertyValue;
            isLtvEligible = ltv.Value <= rules.MaxLtvRatio;
            if (!isLtvEligible)
            {
                unmetConditions.Add($"LTV ratio ({ltv.Value:P1}) exceeds maximum allowed threshold ({rules.MaxLtvRatio:P1}).");
            }
        }

        // 3. Rule Checks
        bool isCreditEligible = facts.EffectiveCreditScore >= rules.MinCreditScore;
        bool isIncomeThresholdEligible = facts.EffectiveMonthlyIncome >= rules.MinMonthlyIncome;
        bool isLoanAmountEligible = facts.RequestedLoanAmount <= rules.MaxLoanAmount;

        if (!isCreditEligible)
        {
            unmetConditions.Add($"Credit score ({facts.EffectiveCreditScore}) is below minimum requirement ({rules.MinCreditScore}).");
        }

        if (!isIncomeThresholdEligible)
        {
            unmetConditions.Add($"Monthly income ({facts.EffectiveMonthlyIncome}) is below product minimum ({rules.MinMonthlyIncome}).");
        }

        if (!isLoanAmountEligible)
        {
            unmetConditions.Add($"Requested loan amount ({facts.RequestedLoanAmount}) exceeds maximum limit ({rules.MaxLoanAmount}).");
        }

        if (rules.RequiresIdentityVerification && !facts.IsIdentityVerified)
        {
            unmetConditions.Add("Applicant identity verification is pending or failed.");
        }

        if (rules.RequiresIncomeVerification && !facts.IsIncomeVerified)
        {
            unmetConditions.Add("Applicant income verification is pending or unconfirmed.");
        }

        // 4. Status Precedence Mapping
        // PendingInformation -> Ineligible -> ReferToHuman -> Eligible
        EligibilityStatus status;

        bool hasPendingVerification = (rules.RequiresIdentityVerification && !facts.IsIdentityVerified) ||
                                       (rules.RequiresIncomeVerification && !facts.IsIncomeVerified);

        if (hasPendingVerification)
        {
            status = EligibilityStatus.PendingInformation;
        }
        else if (income <= 0 ||
                 (rules.RequiresPropertyValuation && propertyValue <= 0) ||
                 !isCreditEligible ||
                 !isIncomeThresholdEligible ||
                 !isLoanAmountEligible)
        {
            status = EligibilityStatus.Ineligible;
        }
        else if (!isDtiEligible || (rules.RequiresPropertyValuation && !isLtvEligible))
        {
            status = EligibilityStatus.ReferToHuman;
        }
        else
        {
            status = EligibilityStatus.Eligible;
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
