using Loan.Domain.Common;

namespace Loan.Domain.Applications;

public class ApplicantFacts
{
    public string ApplicantId { get; }
    public string FullName { get; private set; }
    public string SyntheticId { get; private set; }
    
    // Financial Stated/Extracted Facts
    public Money MonthlyGrossIncome { get; private set; }
    public Money MonthlyDebts { get; private set; }
    public Money RequestedLoanAmount { get; private set; }
    public Money EstimatedPropertyValue { get; private set; }
    public int CreditScore { get; private set; }
    public string EmploymentStatus { get; private set; }
    public string LoanPurpose { get; private set; }

    // Verification Tool Outputs (BR-02: Must come from timestamped tools or confirmed fields)
    public bool IsIdentityVerified { get; private set; }
    public DateTime? IdentityVerifiedAtUtc { get; private set; }
    
    public bool IsIncomeVerified { get; private set; }
    public Money? VerifiedMonthlyIncome { get; private set; }
    public DateTime? IncomeVerifiedAtUtc { get; private set; }

    public bool IsCreditVerified { get; private set; }
    public int? VerifiedCreditScore { get; private set; }
    public DateTime? CreditVerifiedAtUtc { get; private set; }

    public ApplicantFacts(
        string applicantId,
        string fullName,
        string syntheticId,
        Money monthlyGrossIncome,
        Money monthlyDebts,
        Money requestedLoanAmount,
        Money estimatedPropertyValue,
        int creditScore,
        string employmentStatus,
        string loanPurpose)
    {
        if (string.IsNullOrWhiteSpace(applicantId)) throw new ArgumentException("ApplicantId is required.", nameof(applicantId));
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("FullName is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(syntheticId)) throw new ArgumentException("SyntheticId is required.", nameof(syntheticId));

        ApplicantId = applicantId;
        FullName = fullName;
        SyntheticId = syntheticId;
        MonthlyGrossIncome = monthlyGrossIncome;
        MonthlyDebts = monthlyDebts;
        RequestedLoanAmount = requestedLoanAmount;
        EstimatedPropertyValue = estimatedPropertyValue;
        CreditScore = creditScore;
        EmploymentStatus = employmentStatus;
        LoanPurpose = loanPurpose;
    }

    public void SetIdentityVerified(bool isVerified, DateTime timestampUtc)
    {
        IsIdentityVerified = isVerified;
        IdentityVerifiedAtUtc = timestampUtc;
    }

    public void SetIncomeVerified(bool isVerified, Money verifiedIncome, DateTime timestampUtc)
    {
        IsIncomeVerified = isVerified;
        VerifiedMonthlyIncome = verifiedIncome;
        IncomeVerifiedAtUtc = timestampUtc;
    }

    public void SetCreditVerified(bool isVerified, int verifiedCreditScore, DateTime timestampUtc)
    {
        IsCreditVerified = isVerified;
        VerifiedCreditScore = verifiedCreditScore;
        CreditVerifiedAtUtc = timestampUtc;
    }

    /// <summary>
    /// Returns the effective monthly income (verified if available, otherwise stated).
    /// </summary>
    public Money EffectiveMonthlyIncome => IsIncomeVerified && VerifiedMonthlyIncome.HasValue 
        ? VerifiedMonthlyIncome.Value 
        : MonthlyGrossIncome;

    /// <summary>
    /// Returns the effective credit score (verified if available, otherwise stated).
    /// </summary>
    public int EffectiveCreditScore => IsCreditVerified && VerifiedCreditScore.HasValue 
        ? VerifiedCreditScore.Value 
        : CreditScore;
}
