using LoanSystem.Modules.Products.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class InterestRateTests
{
    [Theory]
    // TEA %, expected monthly rate rounded to 10 decimals: (1+TEA)^(1/12) - 1
    [InlineData(12, 0.0094887929)]
    [InlineData(30, 0.0221044506)]
    [InlineData(60, 0.0399441077)]
    [InlineData(100, 0.0594630944)]
    public void ToMonthlyRate_Efectiva_ShouldMatchCompoundFormula(decimal tea, decimal expected)
    {
        var monthly = InterestRate.ToMonthlyRate(tea, RateType.Efectiva);

        Math.Round(monthly, 10).ShouldBe(expected);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(35.5)]
    [InlineData(250)]
    public void ToMonthlyRate_Efectiva_CompoundedTwelveTimes_ShouldReturnTheTea(decimal tea)
    {
        var monthly = InterestRate.ToMonthlyRate(tea, RateType.Efectiva);

        var compounded = InterestRate.Pow(1m + monthly, 12) - 1m;

        Math.Abs(compounded - tea / 100m).ShouldBeLessThan(0.000000000000000001m);
    }

    [Fact]
    public void ToMonthlyRate_Nominal_ShouldDivideBy12()
    {
        InterestRate.ToMonthlyRate(12m, RateType.Nominal).ShouldBe(0.01m);
    }

    [Fact]
    public void ToMonthlyRate_Zero_ShouldBeZero()
    {
        InterestRate.ToMonthlyRate(0m, RateType.Efectiva).ShouldBe(0m);
        InterestRate.ToMonthlyRate(0m, RateType.Nominal).ShouldBe(0m);
    }

    [Fact]
    public void ToMonthlyRate_Negative_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => InterestRate.ToMonthlyRate(-1m, RateType.Efectiva));
    }

    [Theory]
    [InlineData("EFECTIVA", "Efectiva")]
    [InlineData(" tea ", "Efectiva")]
    [InlineData("NOMINAL", "Nominal")]
    [InlineData("tna", "Nominal")]
    public void Parser_ShouldAcceptKnownValues(string value, string expected)
    {
        RateTypeParser.TryParse(value, out var rateType).ShouldBeTrue();
        rateType.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("EFFECTIVE")]
    [InlineData("")]
    [InlineData(null)]
    public void Parser_ShouldRejectUnknownValues(string? value)
    {
        RateTypeParser.TryParse(value, out _).ShouldBeFalse();
    }
}
