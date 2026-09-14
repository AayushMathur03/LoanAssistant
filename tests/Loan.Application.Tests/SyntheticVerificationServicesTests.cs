using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class SyntheticVerificationServicesTests
{
    private SyntheticIdentityService _identityService = null!;
    private SyntheticIncomeService _incomeService = null!;
    private SyntheticCreditService _creditService = null!;

    [SetUp]
    public void SetUp()
    {
        _identityService = new SyntheticIdentityService();
        _incomeService = new SyntheticIncomeService();
        _creditService = new SyntheticCreditService();
    }

    [TestCase("SYN-888777", true, "Alice Cooper")]
    [TestCase("SYN-123456", true, "John Doe")]
    [TestCase("SYN-654321", true, "Jane Smith")]
    [TestCase("SYN-000000", false, "Unknown Applicant")]
    [TestCase("SYN-999999", false, "Unverified Applicant")]
    public async Task IdentityService_VerifyIdentityAsync_ReturnsExpectedResult(string syntheticId, bool expectedVerified, string expectedName)
    {
        var result = await _identityService.VerifyIdentityAsync(syntheticId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.SyntheticId, Is.EqualTo(syntheticId));
        Assert.That(result.IsVerified, Is.EqualTo(expectedVerified));
        Assert.That(result.LegalName, Is.EqualTo(expectedName));
    }

    [TestCase("SYN-888777", true, 12000)]
    [TestCase("SYN-123456", true, 10000)]
    [TestCase("SYN-000000", false, 0)]
    public async Task IncomeService_VerifyIncomeAsync_ReturnsExpectedResult(string syntheticId, bool expectedVerified, decimal expectedAmount)
    {
        var result = await _incomeService.VerifyIncomeAsync(syntheticId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.SyntheticId, Is.EqualTo(syntheticId));
        Assert.That(result.IsVerified, Is.EqualTo(expectedVerified));
        Assert.That(result.VerifiedMonthlyIncome.Amount, Is.EqualTo(expectedAmount));
    }

    [TestCase("SYN-888777", true, 750)]
    [TestCase("SYN-654321", true, 610)]
    [TestCase("SYN-000000", false, 520)]
    public async Task CreditService_GetCreditScoreAsync_ReturnsExpectedResult(string syntheticId, bool expectedVerified, int expectedScore)
    {
        var result = await _creditService.GetCreditScoreAsync(syntheticId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.SyntheticId, Is.EqualTo(syntheticId));
        Assert.That(result.IsVerified, Is.EqualTo(expectedVerified));
        Assert.That(result.CreditScore, Is.EqualTo(expectedScore));
    }
}
