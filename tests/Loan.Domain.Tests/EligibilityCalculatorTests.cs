using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;

namespace Loan.Domain.Tests;

[TestFixture]
public class EligibilityCalculatorTests
{
    private ProductRules _standardRules = null!;

    [SetUp]
    public void SetUp()
    {
        _standardRules = ProductRules.CreateStandardMortgage("v1.0");
    }

    [Test]
    public void Evaluate_EligibleApplicant_ShouldReturnEligibleStatus()
    {
        // Gross Income: 10,000, Debts: 2,000 => DTI = 20% (< 43% max)
        // Loan Amount: 400,000, Property Value: 600,000 => LTV = 66.7% (< 80% max)
        // Credit Score: 720 (>= 640 min)
        var facts = new ApplicantFacts(
            applicantId: "APP-001",
            fullName: "John Doe",
            syntheticId: "SYN-123456",
            monthlyGrossIncome: new Money(10000m),
            monthlyDebts: new Money(2000m),
            requestedLoanAmount: new Money(400000m),
            estimatedPropertyValue: new Money(600000m),
            creditScore: 720,
            employmentStatus: "Full-Time",
            loanPurpose: "Primary Residence"
        );

        var now = DateTime.UtcNow;
        facts.SetIdentityVerified(true, now);
        facts.SetIncomeVerified(true, new Money(10000m), now);
        facts.SetCreditVerified(true, 720, now);

        var result = EligibilityCalculator.Evaluate(facts, _standardRules, now);

        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Eligible));
        Assert.That(result.IsDtiEligible, Is.True);
        Assert.That(result.IsLtvEligible, Is.True);
        Assert.That(result.IsCreditScoreEligible, Is.True);
        Assert.That(result.UnmetConditions, Is.Empty);
        Assert.That(result.DebtToIncomeRatio, Is.EqualTo(0.20m));
        Assert.That(result.LoanToValueRatio, Is.EqualTo(0.6667m));
    }

    [Test]
    public void Evaluate_ExcessiveDti_ShouldReturnReferToHuman()
    {
        // Income: 10,000, Debts: 5,000 => DTI = 50% (> 43% max)
        var facts = new ApplicantFacts(
            applicantId: "APP-002",
            fullName: "Jane Smith",
            syntheticId: "SYN-654321",
            monthlyGrossIncome: new Money(10000m),
            monthlyDebts: new Money(5000m),
            requestedLoanAmount: new Money(400000m),
            estimatedPropertyValue: new Money(600000m),
            creditScore: 720,
            employmentStatus: "Full-Time",
            loanPurpose: "Primary Residence"
        );

        var now = DateTime.UtcNow;
        facts.SetIdentityVerified(true, now);
        facts.SetIncomeVerified(true, new Money(10000m), now);

        var result = EligibilityCalculator.Evaluate(facts, _standardRules, now);

        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.ReferToHuman));
        Assert.That(result.IsDtiEligible, Is.False);
        Assert.That(result.UnmetConditions, Has.Some.Contains("DTI ratio"));
    }

    [Test]
    public void Evaluate_UnverifiedIdentityAndIncome_ShouldIncludeUnmetConditions()
    {
        var facts = new ApplicantFacts(
            applicantId: "APP-003",
            fullName: "Bob Ross",
            syntheticId: "SYN-999999",
            monthlyGrossIncome: new Money(10000m),
            monthlyDebts: new Money(2000m),
            requestedLoanAmount: new Money(300000m),
            estimatedPropertyValue: new Money(500000m),
            creditScore: 700,
            employmentStatus: "Self-Employed",
            loanPurpose: "Refinance"
        );

        var now = DateTime.UtcNow;
        var result = EligibilityCalculator.Evaluate(facts, _standardRules, now);

        Assert.That(result.IsIdentityVerified, Is.False);
        Assert.That(result.IsIncomeVerified, Is.False);
        Assert.That(result.UnmetConditions, Has.Some.Contains("identity verification"));
        Assert.That(result.UnmetConditions, Has.Some.Contains("income verification"));
    }
}
