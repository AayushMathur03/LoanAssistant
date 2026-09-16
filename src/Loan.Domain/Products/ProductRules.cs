using Loan.Domain.Common;

namespace Loan.Domain.Products;

public class ProductRules
{
    public string ProductId { get; }
    public string ProductName { get; }
    public string EffectiveVersion { get; }
    public decimal MaxDtiRatio { get; }          // e.g. 0.43 (43%)
    public decimal MaxLtvRatio { get; }          // e.g. 0.80 (80%)
    public int MinCreditScore { get; }           // e.g. 620
    public Money MinMonthlyIncome { get; }
    public Money MaxLoanAmount { get; }
    public bool RequiresIncomeVerification { get; }
    public bool RequiresIdentityVerification { get; }
    public bool RequiresPropertyValuation { get; }

    public ProductRules(
        string productId,
        string productName,
        string effectiveVersion,
        decimal maxDtiRatio,
        decimal maxLtvRatio,
        int minCreditScore,
        Money minMonthlyIncome,
        Money maxLoanAmount,
        bool requiresIncomeVerification = true,
        bool requiresIdentityVerification = true,
        bool requiresPropertyValuation = true)
    {
        if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("Product ID is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product Name is required.", nameof(productName));
        if (string.IsNullOrWhiteSpace(effectiveVersion)) throw new ArgumentException("Effective version is required.", nameof(effectiveVersion));
        if (maxDtiRatio <= 0 || maxDtiRatio > 1) throw new ArgumentOutOfRangeException(nameof(maxDtiRatio), "DTI ratio must be between 0 and 1.");
        if (maxLtvRatio <= 0 || maxLtvRatio > 1.5m) throw new ArgumentOutOfRangeException(nameof(maxLtvRatio), "LTV ratio must be between 0 and 1.5.");
        if (minCreditScore < 300 || minCreditScore > 850) throw new ArgumentOutOfRangeException(nameof(minCreditScore), "Credit score must be between 300 and 850.");

        ProductId = productId;
        ProductName = productName;
        EffectiveVersion = effectiveVersion;
        MaxDtiRatio = maxDtiRatio;
        MaxLtvRatio = maxLtvRatio;
        MinCreditScore = minCreditScore;
        MinMonthlyIncome = minMonthlyIncome;
        MaxLoanAmount = maxLoanAmount;
        RequiresIncomeVerification = requiresIncomeVerification;
        RequiresIdentityVerification = requiresIdentityVerification;
        RequiresPropertyValuation = requiresPropertyValuation;
    }

    public static ProductRules CreateStandardMortgage(string version = "v1.2") => new(
        productId: "MORTGAGE-STD",
        productName: "Standard Residential Mortgage",
        effectiveVersion: version,
        maxDtiRatio: 0.43m,
        maxLtvRatio: 0.80m,
        minCreditScore: 640,
        minMonthlyIncome: new Money(3000m),
        maxLoanAmount: new Money(750000m),
        requiresIncomeVerification: true,
        requiresIdentityVerification: true,
        requiresPropertyValuation: true
    );

    public static ProductRules CreatePersonalLoan(string version = "v2.0") => new(
        productId: "LOAN-PERSONAL",
        productName: "Personal Loan",
        effectiveVersion: version,
        maxDtiRatio: 0.38m,
        maxLtvRatio: 1.00m,
        minCreditScore: 600,
        minMonthlyIncome: new Money(2500m),
        maxLoanAmount: new Money(75000m),
        requiresIncomeVerification: true,
        requiresIdentityVerification: true,
        requiresPropertyValuation: false
    );

    public static ProductRules CreateAutoLoan(string version = "v1.1") => new(
        productId: "LOAN-AUTO",
        productName: "Vehicle Auto Loan",
        effectiveVersion: version,
        maxDtiRatio: 0.45m,
        maxLtvRatio: 0.90m,
        minCreditScore: 620,
        minMonthlyIncome: new Money(2000m),
        maxLoanAmount: new Money(60000m),
        requiresIncomeVerification: true,
        requiresIdentityVerification: true,
        requiresPropertyValuation: true
    );
}
