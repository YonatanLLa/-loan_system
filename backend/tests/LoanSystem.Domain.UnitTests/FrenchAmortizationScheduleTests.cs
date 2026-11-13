using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class FrenchAmortizationScheduleTests
{
    [Fact]
    public void CalculateInstallment_ShouldMatchStandardFrenchQuota()
    {
        // 10,000 at 12% annual (1% monthly) over 12 months => 888.49
        var quota = FrenchAmortizationSchedule.CalculateInstallment(Money.From(10000m), 0.01m, 12);

        quota.Amount.ShouldBe(888.49m);
    }

    [Fact]
    public void Build_WithNominalRate_ShouldUseTnaDividedBy12()
    {
        // TNA 12% => 1% monthly => 888.49
        var schedule = FrenchAmortizationSchedule.Build(Money.From(10000m), 12m, 12, RateType.Nominal);

        schedule.Installments.Count.ShouldBe(12);
        schedule.Installments.Take(11).ShouldAllBe(i => i.Total.Amount == 888.49m);
        schedule.Installments.Sum(i => i.Principal.Amount).ShouldBe(10000m);
        schedule.Installments.Last().RemainingBalance.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Build_WithEffectiveRate_ShouldUseCompoundMonthlyRate()
    {
        // TEA 12% => i = 1.12^(1/12) - 1 = 0.0094887929... => quota 885.62 (NOT 888.49)
        var schedule = FrenchAmortizationSchedule.Build(Money.From(10000m), 12m, 12, RateType.Efectiva);

        schedule.Installments[0].Interest.Amount.ShouldBe(94.89m);
        schedule.Installments.Take(11).ShouldAllBe(i => i.Total.Amount == 885.62m);
    }

    [Fact]
    public void Build_ShouldAmortizeFullPrincipal_AndEndWithZeroBalance()
    {
        var schedule = FrenchAmortizationSchedule.Build(Money.From(10000m), 12m, 12, RateType.Efectiva);

        schedule.Installments.Count.ShouldBe(12);
        schedule.Installments.Sum(i => i.Principal.Amount).ShouldBe(10000m);
        schedule.Installments.Last().RemainingBalance.Amount.ShouldBe(0m);
        schedule.TotalPayment.Amount.ShouldBe(schedule.Principal.Amount + schedule.TotalInterest.Amount);
        schedule.Installments.Sum(i => i.Interest.Amount).ShouldBe(schedule.TotalInterest.Amount);
    }

    [Fact]
    public void Build_WithZeroRate_ShouldSplitPrincipalEvenly_AndAdjustLastInstallment()
    {
        var schedule = FrenchAmortizationSchedule.Build(Money.From(1000m), 0m, 3, RateType.Efectiva);

        schedule.Installments[0].Total.Amount.ShouldBe(333.33m);
        schedule.Installments[1].Total.Amount.ShouldBe(333.33m);
        schedule.Installments[2].Total.Amount.ShouldBe(333.34m);
        schedule.TotalInterest.Amount.ShouldBe(0m);
        schedule.Installments.Last().RemainingBalance.Amount.ShouldBe(0m);
    }

    [Theory]
    [InlineData("FRENCH")]
    [InlineData("french")]
    [InlineData(" French ")]
    public void Parser_ShouldAcceptFrench_CaseInsensitive(string value)
    {
        AmortizationMethodParser.TryParse(value, out var method).ShouldBeTrue();
        method.ShouldBe(AmortizationMethod.French);
    }

    [Theory]
    [InlineData("GERMAN")]
    [InlineData("")]
    [InlineData(null)]
    public void Parser_ShouldRejectUnknownMethods(string? value)
    {
        AmortizationMethodParser.TryParse(value, out _).ShouldBeFalse();
    }
}
