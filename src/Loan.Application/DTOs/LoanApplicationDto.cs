using Loan.Domain.Applications;
using Loan.Domain.Eligibility;

namespace Loan.Application.DTOs;

public record ApplicantFactsDto(
    string ApplicantId,
    string FullName,
    string SyntheticId,
    decimal MonthlyGrossIncome,
    decimal MonthlyDebts,
    decimal RequestedLoanAmount,
    decimal EstimatedPropertyValue,
    int CreditScore,
    string EmploymentStatus,
    string LoanPurpose,
    bool IsIdentityVerified,
    bool IsIncomeVerified,
    bool IsCreditVerified);

public record EligibilityIndicatorsDto(
    decimal? DebtToIncomeRatio,
    decimal? LoanToValueRatio,
    bool IsDtiEligible,
    bool IsLtvEligible,
    bool IsCreditScoreEligible,
    bool IsIncomeThresholdEligible,
    bool IsLoanAmountEligible,
    EligibilityStatus Status,
    IReadOnlyList<string> UnmetConditions);

public record LoanApplicationDto(
    string ApplicationId,
    string ApplicantId,
    string ProductId,
    ApplicationStatus Status,
    ApplicantFactsDto Facts,
    EligibilityIndicatorsDto? Indicators,
    RecommendationDto? CurrentRecommendation,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
