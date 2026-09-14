using Loan.Application.Abstractions;
using Loan.Domain.Common;

namespace Loan.Infrastructure.Verification;

public class SyntheticIdentityService : IIdentityReader
{
    private readonly Dictionary<string, (bool Verified, string LegalName, string Status)> _records = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYN-888777"] = (true, "Alice Cooper", "Verified - Synthetic ID & SSN Match"),
        ["SYN-123456"] = (true, "John Doe", "Verified - Driver License & SSN Match"),
        ["SYN-654321"] = (true, "Jane Smith", "Verified - Passport Match"),
        ["SYN-100200"] = (true, "Robert Johnson", "Verified - State Registry Match"),
        ["SYN-200300"] = (true, "Emily Davis", "Verified - National ID Match"),
        ["SYN-300400"] = (true, "Michael Brown", "Verified - Driver License Match"),
        ["SYN-400500"] = (true, "Sarah Wilson", "Verified - Passport Match"),
        ["SYN-500600"] = (true, "David Taylor", "Verified - State Registry Match"),
        ["SYN-600700"] = (true, "Jessica Anderson", "Verified - National ID Match"),
        ["SYN-700800"] = (true, "James Thomas", "Verified - Driver License Match"),
        ["SYN-800900"] = (true, "Amanda Martinez", "Verified - State Registry Match"),
        ["SYN-000000"] = (false, "Unknown Applicant", "Identity record not found in synthetic bureau database")
    };

    public Task<IdentityVerificationResult> VerifyIdentityAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(syntheticId))
        {
            return Task.FromResult(new IdentityVerificationResult("", false, "Unknown", "Invalid synthetic ID parameter", DateTime.UtcNow));
        }

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
            IsVerified: false,
            LegalName: "Unverified Applicant",
            StatusDetails: "Synthetic identity record not found in registry",
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
        ["SYN-100200"] = (true, 15000m, "Enterprise Systems Corp"),
        ["SYN-200300"] = (true, 9500m, "Finch Financial LLC"),
        ["SYN-300400"] = (true, 11000m, "Metro Health System"),
        ["SYN-400500"] = (true, 13500m, "Apex Software Solutions"),
        ["SYN-500600"] = (true, 8800m, "Pinnacle Retail Group"),
        ["SYN-600700"] = (true, 14200m, "Vanguard Aerospace"),
        ["SYN-700800"] = (true, 9200m, "Summit Logistics"),
        ["SYN-800900"] = (true, 10500m, "Beacon Media Works"),
        ["SYN-000000"] = (false, 0m, "Unverified Employer")
    };

    public Task<IncomeVerificationResult> VerifyIncomeAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(syntheticId))
        {
            return Task.FromResult(new IncomeVerificationResult("", false, new Money(0m), "Unknown", DateTime.UtcNow));
        }

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
            IsVerified: false,
            VerifiedMonthlyIncome: new Money(0m),
            EmployerName: "Unverified Income Registry",
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
        ["SYN-100200"] = (true, 780, "Experian Synthetic Bureau"),
        ["SYN-200300"] = (true, 740, "Equifax Synthetic Bureau"),
        ["SYN-300400"] = (true, 690, "TransUnion Synthetic Bureau"),
        ["SYN-400500"] = (true, 810, "Experian Synthetic Bureau"),
        ["SYN-500600"] = (true, 640, "Equifax Synthetic Bureau"),
        ["SYN-600700"] = (true, 760, "TransUnion Synthetic Bureau"),
        ["SYN-700800"] = (true, 700, "Experian Synthetic Bureau"),
        ["SYN-800900"] = (true, 730, "Equifax Synthetic Bureau"),
        ["SYN-000000"] = (false, 520, "Unverified Credit Bureau")
    };

    public Task<CreditVerificationResult> GetCreditScoreAsync(string syntheticId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(syntheticId))
        {
            return Task.FromResult(new CreditVerificationResult("", false, 0, "Unknown", DateTime.UtcNow));
        }

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
            IsVerified: false,
            CreditScore: 0,
            BureauName: "Unverified Credit Registry",
            CheckedAtUtc: DateTime.UtcNow));
    }
}
