using Loan.Application.Abstractions;
using Loan.Domain.Common;

namespace Loan.Infrastructure.Verification;

public class SyntheticIdentityService : IIdentityReader
{
    private readonly Dictionary<string, (bool Verified, string LegalName, string Status)> _records = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYN-888777"] = (true, "Alice Cooper", "Verified - Synthetic Match"),
        ["SYN-123456"] = (true, "John Doe", "Verified - Synthetic Match"),
        ["SYN-654321"] = (true, "Jane Smith", "Verified - Synthetic Match"),
        ["SYN-000000"] = (false, "Unknown", "Identity record not found in synthetic bureau database")
    };

    public Task<IdentityVerificationResult> VerifyIdentityAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (_records.TryGetValue(syntheticId, out var rec))
        {
            return Task.FromResult(new IdentityVerificationResult(
                SyntheticId: syntheticId,
                IsVerified: rec.Verified,
                LegalName: rec.LegalName,
                StatusDetails: rec.Status,
                VerifiedAtUtc: DateTime.UtcNow));
        }

        return Task.FromResult(new IdentityVerificationResult(
            SyntheticId: syntheticId,
            IsVerified: true, // Default synthetic fallback for test IDs
            LegalName: "Synthetic Applicant",
            StatusDetails: "Verified - Default Synthetic Registry",
            VerifiedAtUtc: DateTime.UtcNow));
    }
}

public class SyntheticIncomeService : IIncomeReader
{
    private readonly Dictionary<string, (bool Verified, decimal Amount, string Employer)> _records = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYN-888777"] = (true, 12000m, "Acme Corporation"),
        ["SYN-123456"] = (true, 10000m, "TechCorp LLC"),
        ["SYN-654321"] = (true, 10000m, "Global Logistics Inc"),
        ["SYN-000000"] = (false, 0m, "Unverified")
    };

    public Task<IncomeVerificationResult> VerifyIncomeAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (_records.TryGetValue(syntheticId, out var rec))
        {
            return Task.FromResult(new IncomeVerificationResult(
                SyntheticId: syntheticId,
                IsVerified: rec.Verified,
                VerifiedMonthlyIncome: new Money(rec.Amount),
                EmployerName: rec.Employer,
                VerifiedAtUtc: DateTime.UtcNow));
        }

        return Task.FromResult(new IncomeVerificationResult(
            SyntheticId: syntheticId,
            IsVerified: true,
            VerifiedMonthlyIncome: new Money(8500m),
            EmployerName: "Verified Synthetic Employer",
            VerifiedAtUtc: DateTime.UtcNow));
    }
}

public class SyntheticCreditService : ICreditReader
{
    private readonly Dictionary<string, (bool Verified, int Score, string Bureau)> _records = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYN-888777"] = (true, 750, "Experian Synthetic Bureau"),
        ["SYN-123456"] = (true, 720, "Equifax Synthetic Bureau"),
        ["SYN-654321"] = (true, 610, "TransUnion Synthetic Bureau"),
        ["SYN-000000"] = (false, 550, "Unverified Credit Bureau")
    };

    public Task<CreditVerificationResult> GetCreditScoreAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (_records.TryGetValue(syntheticId, out var rec))
        {
            return Task.FromResult(new CreditVerificationResult(
                SyntheticId: syntheticId,
                IsVerified: rec.Verified,
                CreditScore: rec.Score,
                BureauName: rec.Bureau,
                CheckedAtUtc: DateTime.UtcNow));
        }

        return Task.FromResult(new CreditVerificationResult(
            SyntheticId: syntheticId,
            IsVerified: true,
            CreditScore: 710,
            BureauName: "Synthetic Credit Bureau",
            CheckedAtUtc: DateTime.UtcNow));
    }
}
