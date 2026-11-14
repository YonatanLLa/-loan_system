using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Products;

public class TceaCalculatorTests
{
    private static ScheduleResult French(decimal tea, int n = 12, Money? fee = null) =>
        ScheduleEngine.Build(new ScheduleInput(
            Money.From(10000m), tea, RateType.Efectiva, PaymentFrequency.MENSUAL, n, AmortizationMethod.FRANCES,
            PeriodicFee: fee)).Value;

    private static List<CashFlow> Flows(ScheduleResult s) =>
        s.Lines.Select(l => new CashFlow(l.Number * 30, l.Total)).ToList();

    [Fact] // CA-T.a / V5: V1 + 2 % deductive opening fee + 5.00 per installment
    public void Tcea_V5()
    {
        var schedule = French(0.30m, fee: Money.From(5m));

        var tcea = TceaCalculator.Calculate(Money.From(9800m), Flows(schedule));

        Math.Round(tcea * 100m, 2).ShouldBe(36.54m);
    }

    [Fact] // CA-T.b: no fees → TCEA ≈ TEA (only centavo rounding differs)
    public void Tcea_WithoutFees_EqualsTea()
    {
        var tcea = TceaCalculator.Calculate(Money.From(10000m), Flows(French(0.30m)));

        Math.Abs(tcea - 0.30m).ShouldBeLessThan(0.0001m);
    }

    [Theory] // convergence with high rates
    [InlineData(3)]    // 300 %
    [InlineData(10)]   // 1000 %
    public void Tcea_ConvergesWithHighRates(decimal tea)
    {
        var tcea = TceaCalculator.Calculate(Money.From(10000m), Flows(French(tea, n: 36)));

        Math.Abs(tcea - tea).ShouldBeLessThan(0.001m);
    }

    [Fact]
    public void Tcea_ZeroCost_IsZero()
    {
        var flows = new List<CashFlow> { new(30, Money.From(500m)), new(60, Money.From(500m)) };

        TceaCalculator.Calculate(Money.From(1000m), flows).ShouldBe(0m);
    }
}
