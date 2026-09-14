namespace Loan.Domain.Eligibility;

public enum EligibilityStatus
{
    NotEvaluated,
    Eligible,
    ReferToHuman,
    Ineligible
}

public class EligibilityIndicators
{
    public decimal DebtToIncomeRatio { get; }      // DTI = Total Monthly Debts / Monthly Income
    public decimal LoanToValueRatio { get; }       // LTV = Requested Loan Amount / Property Value
    
    public bool IsDtiEligible { get; }
    public bool IsLtvEligible { get; }
    public bool IsCreditScoreEligible { get; }
    public bool IsIncomeThresholdEligible { get; }
    public bool IsLoanAmountEligible { get; }

    public bool IsIdentityVerified { get; }
    public bool IsIncomeVerified { get; }
    public bool IsCreditVerified { get; }

    public EligibilityStatus Status { get; }
    public IReadOnlyList<string> UnmetConditions { get; }
    public DateTime EvaluatedAtUtc { get; }

    public EligibilityIndicators(
        decimal debtToIncomeRatio,
        decimal loanToValueRatio,
        bool isDtiEligible,
        bool isLtvEligible,
        bool isCreditScoreEligible,
        bool isIncomeThresholdEligible,
        bool isLoanAmountEligible,
        bool isIdentityVerified,
        bool isIncomeVerified,
        bool isCreditVerified,
        EligibilityStatus status,
        IEnumerable<string> unmetConditions,
        DateTime evaluatedAtUtc)
    {
        DebtToIncomeRatio = Math.Round(debtToIncomeRatio, 4);
        LoanToValueRatio = Math.Round(loanToValueRatio, 4);
        IsDtiEligible = isDtiEligible;
        IsLtvEligible = isLtvEligible;
        IsCreditScoreEligible = isCreditScoreEligible;
        IsIncomeThresholdEligible = isIncomeThresholdEligible;
        IsLoanAmountEligible = isLoanAmountEligible;
        IsIdentityVerified = isIdentityVerified;
        IsIncomeVerified = isIncomeVerified;
        IsCreditVerified = isCreditVerified;
        Status = status;
        UnmetConditions = unmetConditions?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
        EvaluatedAtUtc = evaluatedAtUtc;
    }
}
