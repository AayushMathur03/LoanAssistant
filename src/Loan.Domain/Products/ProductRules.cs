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
        bool requiresIdentityVerification = true)
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
    }

    public static ProductRules CreateStandardMortgage(string version = "v1.0") => new(
        productId: "MORTGAGE-STD",
        productName: "Standard Residential Mortgage",
        effectiveVersion: version,
        maxDtiRatio: 0.43m,
        maxLtvRatio: 0.80m,
        minCreditScore: 640,
        minMonthlyIncome: new Money(3000m),
        maxLoanAmount: new Money(750000m)
    );

    public static ProductRules CreatePersonalLoan(string version = "v1.0") => new(
        productId: "LOAN-PERSONAL",
        productName: "Personal Loan",
        effectiveVersion: version,
        maxDtiRatio: 0.36m,
        maxLtvRatio: 1.00m,
        minCreditScore: 620,
        minMonthlyIncome: new Money(2000m),
        maxLoanAmount: new Money(50000m),
        requiresIncomeVerification: true,
        requiresIdentityVerification: true
    );
}
