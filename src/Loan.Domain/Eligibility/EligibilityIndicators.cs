namespace Loan.Domain.Eligibility;

public enum EligibilityStatus
{
    NotEvaluated,
    PendingInformation,
    Ineligible,
    ReferToHuman,
    Eligible
}

public class EligibilityIndicators
{
    public decimal? DebtToIncomeRatio { get; }      // DTI = Total Monthly Debts / Monthly Income (null if income <= 0)
    public decimal? LoanToValueRatio { get; }       // LTV = Requested Loan Amount / Property Value (null if product does not require property valuation or property value <= 0)
    
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
        decimal? debtToIncomeRatio,
        decimal? loanToValueRatio,
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
        DebtToIncomeRatio = debtToIncomeRatio.HasValue ? Math.Round(debtToIncomeRatio.Value, 4) : null;
        LoanToValueRatio = loanToValueRatio.HasValue ? Math.Round(loanToValueRatio.Value, 4) : null;
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
