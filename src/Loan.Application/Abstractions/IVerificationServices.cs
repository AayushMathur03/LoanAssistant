using Loan.Domain.Common;

namespace Loan.Application.Abstractions;

public record IdentityVerificationResult(
    string SyntheticId,
    bool IsVerified,
    string LegalName,
    string StatusDetails,
    DateTime VerifiedAtUtc);

public record IncomeVerificationResult(
    string SyntheticId,
    bool IsVerified,
    Money VerifiedMonthlyIncome,
    string EmployerName,
    DateTime VerifiedAtUtc);

public record CreditVerificationResult(
    string SyntheticId,
    bool IsVerified,
    int CreditScore,
    string BureauName,
    DateTime CheckedAtUtc);

public interface IIdentityReader
{
    Task<IdentityVerificationResult> VerifyIdentityAsync(string syntheticId, CancellationToken cancellationToken = default);
}

public interface IIncomeReader
{
    Task<IncomeVerificationResult> VerifyIncomeAsync(string syntheticId, CancellationToken cancellationToken = default);
}

public interface ICreditReader
{
    Task<CreditVerificationResult> GetCreditScoreAsync(string syntheticId, CancellationToken cancellationToken = default);
}
