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

    [Theory] // PLAN_PRODUCTOS §12: (1.30)^(d/360) − 1, in percent with 8 decimals
    [InlineData(1, 0.07290553)]
    [InlineData(7, 0.51145623)]
    [InlineData(15, 1.09918153)]
    [InlineData(30, 2.21044506)]
    public void ToPeriodRate_Efectiva_Tea30_ShouldMatchReferenceTable(int days, decimal expectedPercent)
    {
        var rate = InterestRate.ToPeriodRate(0.30m, RateType.Efectiva, days);

        Math.Round(rate * 100m, 8).ShouldBe(expectedPercent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(24)]
    [InlineData(31)]
    public void ToPeriodRate_Efectiva_RoundTrip_ShouldReturnTheTea(int days)
    {
        var rate = InterestRate.ToPeriodRate(0.30m, RateType.Efectiva, days);

        // (1 + i_d)^(360/d) − 1 = TEA; for d not dividing 360 check the daily factor instead.
        var daily = InterestRate.Pow(1m + rate, 360 / Gcd(days, 360));
        var expected = InterestRate.Pow(1.30m, days / Gcd(days, 360));
        Math.Abs(daily - expected).ShouldBeLessThan(0.000000000000000001m);
    }

    [Fact]
    public void ToPeriodRate_With30Days_IsTheMonthlyRate()
    {
        InterestRate.ToPeriodRate(0.12m, RateType.Efectiva, 30).ShouldBe(InterestRate.ToMonthlyRate(12m, RateType.Efectiva));
        InterestRate.ToPeriodRate(0.12m, RateType.Nominal, 30).ShouldBe(0.01m);
    }

    [Fact]
    public void ToPeriodRate_Nominal_IsProportionalTo360Days()
    {
        InterestRate.ToPeriodRate(0.36m, RateType.Nominal, 1).ShouldBe(0.001m);
        InterestRate.ToPeriodRate(0.36m, RateType.Nominal, 15).ShouldBe(0.015m);
    }

    [Fact]
    public void DailyGrowthFactor_PowerOf360_ShouldReturnOnePlusTea()
    {
        var q = InterestRate.DailyGrowthFactor(0.30m);

        Math.Abs(InterestRate.Pow(q, 360) - 1.30m).ShouldBeLessThan(0.000000000000000001m);
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

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
