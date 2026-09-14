using Loan.Application.Common;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class PiiMaskerTests
{
    [Test]
    public void MaskSsn_StandardSsn_MasksFirstFiveDigits()
    {
        var input = "123-45-6789";
        var masked = PiiMasker.MaskSsn(input);
        Assert.That(masked, Is.EqualTo("***-**-6789"));
    }

    [Test]
    public void MaskPii_TextWithSsnAccountAndEmail_RedactsAllPii()
    {
        var rawText = "Applicant John Doe (SSN: 987-65-4321, Account: 9876543210, Email: john.doe@example.com) requested loan review.";
        var masked = PiiMasker.MaskPii(rawText);

        Assert.That(masked, Does.Not.Contain("987-65-4321"));
        Assert.That(masked, Does.Not.Contain("9876543210"));
        Assert.That(masked, Does.Not.Contain("john.doe@example.com"));
        Assert.That(masked, Does.Contain("***-**-4321"));
        Assert.That(masked, Does.Contain("*****3210"));
        Assert.That(masked, Does.Contain("jo***@example.com"));
    }

    [Test]
    public void MaskPii_NullOrEmptyInput_ReturnsEmptyString()
    {
        Assert.That(PiiMasker.MaskPii(null), Is.EqualTo(string.Empty));
        Assert.That(PiiMasker.MaskPii("   "), Is.EqualTo(string.Empty));
    }
}
