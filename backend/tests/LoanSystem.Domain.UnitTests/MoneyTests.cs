using LoanSystem.Common.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Round_ShouldApplyMidpointRoundingAwayFromZero_HalfUp()
    {
        // Half-up rounding (0.005 rounds up away from zero to 0.01)
        Money.Round(2.345m).ShouldBe(2.35m);
        Money.Round(2.344m).ShouldBe(2.34m);
        Money.Round(2.346m).ShouldBe(2.35m);
        Money.Round(100.125m).ShouldBe(100.13m);

        // Negative values round away from zero
        Money.Round(-2.345m).ShouldBe(-2.35m);
        Money.Round(-2.344m).ShouldBe(-2.34m);
    }

    [Fact]
    public void Constructor_ShouldRoundAmountAutomatically()
    {
        var money = new Money(1500.555m, "PEN");

        money.Amount.ShouldBe(1500.56m);
        money.Currency.ShouldBe("PEN");
    }

    [Fact]
    public void Addition_ShouldSumAmounts_WhenCurrenciesMatch()
    {
        var m1 = new Money(100.50m, "PEN");
        var m2 = new Money(50.25m, "PEN");

        var result = m1 + m2;

        result.Amount.ShouldBe(150.75m);
        result.Currency.ShouldBe("PEN");
    }

    [Fact]
    public void Addition_ShouldThrowException_WhenCurrenciesDiffer()
    {
        var m1 = new Money(100m, "PEN");
        var m2 = new Money(50m, "USD");

        Should.Throw<InvalidOperationException>(() =>
        {
            var _ = m1 + m2;
        });
    }

    [Fact]
    public void Subtraction_ShouldSubtractAmounts()
    {
        var m1 = new Money(100m, "PEN");
        var m2 = new Money(40.25m, "PEN");

        var result = m1 - m2;

        result.Amount.ShouldBe(59.75m);
    }

    [Fact]
    public void Multiply_ShouldMultiplyAndRoundCorrectly()
    {
        var money = new Money(100m, "PEN");
        var result = money * 0.1555m; // 15.55

        result.Amount.ShouldBe(15.55m);
    }

    [Fact]
    public void Divide_ShouldDivideAndRoundCorrectly()
    {
        var money = new Money(100m, "PEN");
        var result = money / 3m; // 33.3333... -> 33.33

        result.Amount.ShouldBe(33.33m);
    }

    [Fact]
    public void Divide_ByZero_ShouldThrowException()
    {
        var money = new Money(100m, "PEN");
        Should.Throw<DivideByZeroException>(() => money / 0m);
    }

    [Fact]
    public void Equality_ShouldBeTrue_WhenAmountAndCurrencyAreEqual()
    {
        var m1 = new Money(500m, "PEN");
        var m2 = new Money(500.00m, "pen");

        m1.ShouldBe(m2);
        (m1 == m2).ShouldBeTrue();
    }
}
