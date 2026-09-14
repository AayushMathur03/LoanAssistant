using Loan.Domain.Common;

namespace Loan.Domain.Tests;

[TestFixture]
public class MoneyTests
{
    [Test]
    public void Money_Creation_ValidAmount_ShouldFormatCorrectly()
    {
        var money = new Money(1250.50m, "USD");
        Assert.That(money.Amount, Is.EqualTo(1250.50m));
        Assert.That(money.Currency, Is.EqualTo("USD"));
        Assert.That(money.ToString(), Is.EqualTo("USD 1,250.50"));
    }

    [Test]
    public void Money_Creation_NegativeAmount_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-100m));
    }

    [Test]
    public void Money_Addition_SameCurrency_ShouldSumCorrectly()
    {
        var m1 = new Money(500m);
        var m2 = new Money(250m);
        var sum = m1 + m2;

        Assert.That(sum.Amount, Is.EqualTo(750m));
        Assert.That(sum.Currency, Is.EqualTo("USD"));
    }

    [Test]
    public void Money_Addition_DifferentCurrencies_ShouldThrowInvalidOperationException()
    {
        var m1 = new Money(500m, "USD");
        var m2 = new Money(250m, "EUR");

        Assert.Throws<InvalidOperationException>(() => _ = m1 + m2);
    }
}
