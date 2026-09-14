using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using NUnit.Framework;

namespace Loan.Domain.Tests;

[TestFixture]
public class DeterministicEligibilityEngineTests
{
    private ProductRules _mortgageRules = null!;
    private ProductRules _personalLoanRules = null!;

    [SetUp]
    public void SetUp()
    {
        _mortgageRules = ProductRules.CreateStandardMortgage("v1.2");
        _personalLoanRules = ProductRules.CreatePersonalLoan("v2.0");
    }

    [Test]
    public void DtiExactThreshold_PassesAtBoundary_RefersAboveBoundary()
    {
        // 43% DTI threshold for Mortgage v1.2 ($4,300 debt / $10,000 income = 43.0%)
        var factsExact = CreateFacts(monthlyGrossIncome: 10000m, monthlyDebts: 4300m);
        var evalExact = EligibilityCalculator.Evaluate(factsExact, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalExact.DebtToIncomeRatio, Is.EqualTo(0.4300m));
        Assert.That(evalExact.IsDtiEligible, Is.True);
        Assert.That(evalExact.Status, Is.EqualTo(EligibilityStatus.Eligible));

        // Just above boundary ($4,310 debt / $10,000 income = 43.1%)
        var factsAbove = CreateFacts(monthlyGrossIncome: 10000m, monthlyDebts: 4310m);
        var evalAbove = EligibilityCalculator.Evaluate(factsAbove, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalAbove.DebtToIncomeRatio, Is.EqualTo(0.4310m));
        Assert.That(evalAbove.IsDtiEligible, Is.False);
        Assert.That(evalAbove.Status, Is.EqualTo(EligibilityStatus.ReferToHuman));
    }

    [Test]
    public void LtvExactThreshold_PassesAtBoundary_RefersAboveBoundary()
    {
        // 80% LTV threshold for Mortgage v1.2 ($400,000 loan / $500,000 property = 80.0%)
        var factsExact = CreateFacts(requestedLoanAmount: 400000m, estimatedPropertyValue: 500000m);
        var evalExact = EligibilityCalculator.Evaluate(factsExact, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalExact.LoanToValueRatio, Is.EqualTo(0.8000m));
        Assert.That(evalExact.IsLtvEligible, Is.True);
        Assert.That(evalExact.Status, Is.EqualTo(EligibilityStatus.Eligible));

        // Just above boundary ($401,000 loan / $500,000 property = 80.2%)
        var factsAbove = CreateFacts(requestedLoanAmount: 401000m, estimatedPropertyValue: 500000m);
        var evalAbove = EligibilityCalculator.Evaluate(factsAbove, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalAbove.LoanToValueRatio, Is.EqualTo(0.8020m));
        Assert.That(evalAbove.IsLtvEligible, Is.False);
        Assert.That(evalAbove.Status, Is.EqualTo(EligibilityStatus.ReferToHuman));
    }

    [Test]
    public void CreditScoreExactThreshold_PassesAtBoundary_FailsBelowBoundary()
    {
        // 640 min credit score threshold for Mortgage v1.2
        var factsExact = CreateFacts(creditScore: 640);
        var evalExact = EligibilityCalculator.Evaluate(factsExact, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalExact.IsCreditScoreEligible, Is.True);
        Assert.That(evalExact.Status, Is.EqualTo(EligibilityStatus.Eligible));

        // Just below boundary (639 credit score)
        var factsBelow = CreateFacts(creditScore: 639);
        var evalBelow = EligibilityCalculator.Evaluate(factsBelow, _mortgageRules, DateTime.UtcNow);

        Assert.That(evalBelow.IsCreditScoreEligible, Is.False);
        Assert.That(evalBelow.Status, Is.EqualTo(EligibilityStatus.Ineligible));
    }

    [Test]
    public void ZeroIncome_ReturnsNullRatio_AndIneligibleStatus()
    {
        var factsZero = CreateFacts(monthlyGrossIncome: 0m, monthlyDebts: 2000m);
        var result = EligibilityCalculator.Evaluate(factsZero, _mortgageRules, DateTime.UtcNow);

        Assert.That(result.DebtToIncomeRatio, Is.Null);
        Assert.That(result.IsDtiEligible, Is.False);
        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Ineligible));
        Assert.That(result.UnmetConditions, Does.Contain("Monthly gross income is zero or invalid ($0.00); DTI calculation cannot be performed."));
    }

    [Test]
    public void ZeroPropertyValue_OnMortgage_ReturnsNullRatio_AndIneligibleStatus()
    {
        var factsZeroProp = CreateFacts(requestedLoanAmount: 300000m, estimatedPropertyValue: 0m);
        var result = EligibilityCalculator.Evaluate(factsZeroProp, _mortgageRules, DateTime.UtcNow);

        Assert.That(result.LoanToValueRatio, Is.Null);
        Assert.That(result.IsLtvEligible, Is.False);
        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Ineligible));
        Assert.That(result.UnmetConditions, Does.Contain("Estimated property value is zero or invalid for mortgage loan; LTV ratio cannot be calculated."));
    }

    [Test]
    public void PersonalLoan_OmitsLtvCalculation_AndPassesLtvCheck()
    {
        var facts = CreateFacts(monthlyGrossIncome: 8000m, monthlyDebts: 1500m, requestedLoanAmount: 20000m, estimatedPropertyValue: 0m, creditScore: 700);
        var result = EligibilityCalculator.Evaluate(facts, _personalLoanRules, DateTime.UtcNow);

        Assert.That(result.LoanToValueRatio, Is.Null);
        Assert.That(result.IsLtvEligible, Is.True);
        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Eligible));
    }

    [Test]
    public void VerifiedPrecedence_VerifiedIncomeOverridesStatedIncome()
    {
        // Stated income $14,200 (DTI would be 31.0%), but Verified income is $8,500 (DTI is 51.8%)
        var facts = CreateFacts(monthlyGrossIncome: 14200m, monthlyDebts: 4400m);
        facts.SetIncomeVerified(true, new Money(8500m), DateTime.UtcNow);

        var result = EligibilityCalculator.Evaluate(facts, _mortgageRules, DateTime.UtcNow);

        Assert.That(result.DebtToIncomeRatio, Is.EqualTo(0.5176m));
        Assert.That(result.IsDtiEligible, Is.False);
        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.ReferToHuman));
    }

    [Test]
    public void VerifiedPrecedence_VerifiedCreditScoreOverridesStatedCreditScore()
    {
        // Stated credit 750 (passes), but Verified credit is 610 (fails)
        var facts = CreateFacts(creditScore: 750);
        facts.SetCreditVerified(true, 610, DateTime.UtcNow);

        var result = EligibilityCalculator.Evaluate(facts, _mortgageRules, DateTime.UtcNow);

        Assert.That(result.IsCreditScoreEligible, Is.False);
        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Ineligible));
    }

    [Test]
    public void StatusSemantics_UnverifiedMandatoryFields_ReturnsPendingInformation()
    {
        var factsUnverified = new ApplicantFacts(
            "APP-100", "Alice Cooper", "SYN-888777",
            new Money(12000m), new Money(2500m), new Money(350000m), new Money(500000m),
            750, "Employed", "Purchase");
        // Identity & Income unverified

        var result = EligibilityCalculator.Evaluate(factsUnverified, _mortgageRules, DateTime.UtcNow);

        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.PendingInformation));
        Assert.That(result.UnmetConditions, Does.Contain("Applicant identity verification is pending or failed."));
    }

    private static ApplicantFacts CreateFacts(
        decimal monthlyGrossIncome = 10000m,
        decimal monthlyDebts = 2000m,
        decimal requestedLoanAmount = 300000m,
        decimal estimatedPropertyValue = 500000m,
        int creditScore = 720)
    {
        var facts = new ApplicantFacts(
            "APP-100", "Alice Cooper", "SYN-888777",
            new Money(monthlyGrossIncome), new Money(monthlyDebts),
            new Money(requestedLoanAmount), new Money(estimatedPropertyValue),
            creditScore, "Employed", "Purchase");

        facts.SetIdentityVerified(true, DateTime.UtcNow);
        facts.SetIncomeVerified(true, new Money(monthlyGrossIncome), DateTime.UtcNow);
        facts.SetCreditVerified(true, creditScore, DateTime.UtcNow);

        return facts;
    }
}
