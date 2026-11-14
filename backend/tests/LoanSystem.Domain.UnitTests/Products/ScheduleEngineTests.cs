using System.Diagnostics;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Products;

/// <summary>BE-PRO-02 acceptance criteria, using the reference values of PLAN_PRODUCTOS §12.</summary>
public class ScheduleEngineTests
{
    private static readonly Money P10000 = Money.From(10000m);

    private static ScheduleResult Build(
        AmortizationMethod method,
        int installments = 12,
        PaymentFrequency frequency = PaymentFrequency.MENSUAL,
        decimal tea = 0.30m,
        Money? principal = null,
        Money? periodicFee = null)
    {
        var result = ScheduleEngine.Build(new ScheduleInput(
            principal ?? P10000, tea, RateType.Efectiva, frequency, installments, method, PeriodicFee: periodicFee));
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    [Fact] // CA-02.a / V1
    public void French_V1()
    {
        var s = Build(AmortizationMethod.FRANCES);

        s.Mode.ShouldBe(DayCountMode.NOMINAL_PERIODS);
        s.Lines.Take(11).ShouldAllBe(l => l.Total.Amount == 957.86m);
        s.Lines[0].Principal.Amount.ShouldBe(736.82m);
        s.Lines[0].Interest.Amount.ShouldBe(221.04m);
        s.Lines[11].Principal.Amount.ShouldBe(937.13m);
        s.Lines[11].Interest.Amount.ShouldBe(20.71m);
        s.Lines[11].Total.Amount.ShouldBe(957.84m);
        s.TotalInterest.Amount.ShouldBe(1494.30m);
        s.InstallmentAmount.Amount.ShouldBe(957.86m);
        s.EffectiveAnnualRate.ShouldBeNull();
        AssertBalanced(s, P10000);
    }

    [Fact] // CA-02.b / V2
    public void German_V2()
    {
        var s = Build(AmortizationMethod.ALEMAN);

        s.Lines.Take(11).ShouldAllBe(l => l.Principal.Amount == 833.33m);
        s.Lines[0].Interest.Amount.ShouldBe(221.04m);
        s.Lines[0].Total.Amount.ShouldBe(1054.37m);
        s.Lines[11].Principal.Amount.ShouldBe(833.37m);
        s.Lines[11].Interest.Amount.ShouldBe(18.42m);
        s.Lines[11].Total.Amount.ShouldBe(851.79m);
        s.TotalInterest.Amount.ShouldBe(1436.76m);
        AssertBalanced(s, P10000);
    }

    [Fact] // CA-02.c / V3
    public void American_V3()
    {
        var s = Build(AmortizationMethod.AMERICANO);

        s.Lines.Take(11).ShouldAllBe(l => l.Total.Amount == 221.04m && l.Principal.Amount == 0m);
        s.Lines[11].Total.Amount.ShouldBe(10221.04m);
        s.TotalInterest.Amount.ShouldBe(2652.48m);
        AssertBalanced(s, P10000);
    }

    [Fact] // CA-02.d / V4 (+ D12: real effective rate)
    public void Flat_V4()
    {
        var s = Build(AmortizationMethod.FLAT);

        s.TotalInterest.Amount.ShouldBe(2652.53m);
        s.Lines.Take(11).ShouldAllBe(l => l.Total.Amount == 1054.38m && l.Principal.Amount == 833.34m && l.Interest.Amount == 221.04m);
        s.Lines[11].Principal.Amount.ShouldBe(833.26m);
        s.Lines[11].Interest.Amount.ShouldBe(221.09m);
        s.Lines[11].Total.Amount.ShouldBe(1054.35m);
        s.EffectiveAnnualRate.ShouldNotBeNull();
        Math.Round(s.EffectiveAnnualRate!.Value * 100m, 2).ShouldBe(56.80m);
        AssertBalanced(s, P10000);
    }

    [Fact] // V9
    public void French_Weekly_V9()
    {
        var s = Build(AmortizationMethod.FRANCES, installments: 52, frequency: PaymentFrequency.SEMANAL);

        s.Lines[0].Days.ShouldBe(7);
        s.InstallmentAmount.Amount.ShouldBe(219.50m);
        AssertBalanced(s, P10000);
    }

    [Fact] // V10 / DECISION_01 values stay the same through the engine
    public void French_Tea12_V10()
    {
        Build(AmortizationMethod.FRANCES, tea: 0.12m).InstallmentAmount.Amount.ShouldBe(885.62m);
    }

    [Fact] // CA-06.c / V6: ACTUAL_360 with an irregular first period (dates provided by BE-PRO-06)
    public void French_Actual360_V6()
    {
        var disbursement = new DateOnly(2026, 11, 14);
        var dueDates = Enumerable.Range(0, 12)
            .Select(k => new DateOnly(2026, 12, 8).AddMonths(k))
            .Select(d => new DueDatePair(d, d))
            .ToList();

        var result = ScheduleEngine.Build(new ScheduleInput(
            P10000, 0.30m, RateType.Efectiva, PaymentFrequency.MENSUAL, 12, AmortizationMethod.FRANCES,
            DisbursementDate: disbursement, DueDates: dueDates));

        result.IsSuccess.ShouldBeTrue();
        var s = result.Value;
        s.Mode.ShouldBe(DayCountMode.ACTUAL_360);
        s.InstallmentAmount.Amount.ShouldBe(955.06m);

        s.Lines[0].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 12, 8));
        s.Lines[0].Days.ShouldBe(24);
        s.Lines[0].Principal.Amount.ShouldBe(778.61m);
        s.Lines[0].Interest.Amount.ShouldBe(176.45m);

        s.Lines[1].Days.ShouldBe(31);
        s.Lines[1].Principal.Amount.ShouldBe(744.35m);
        s.Lines[1].Interest.Amount.ShouldBe(210.71m);

        s.Lines[11].DueDate.ShouldBe(new DateOnly(2027, 11, 8));
        s.Lines[11].Principal.Amount.ShouldBe(933.69m);
        s.Lines[11].Interest.Amount.ShouldBe(21.33m);
        s.Lines[11].Total.Amount.ShouldBe(955.02m);
        s.TotalInterest.Amount.ShouldBe(1460.68m);
        AssertBalanced(s, P10000);
    }

    [Fact] // D4: interest is accrued up to the theoretical date, the shifted due date does not change it
    public void Actual360_ShiftedDueDate_DoesNotChangeInterest()
    {
        var disbursement = new DateOnly(2026, 11, 14);
        var theoretical = Enumerable.Range(0, 12).Select(k => new DateOnly(2026, 12, 8).AddMonths(k)).ToList();
        var plain = theoretical.Select(d => new DueDatePair(d, d)).ToList();
        var shifted = theoretical.Select((d, i) => new DueDatePair(d, i == 0 ? d.AddDays(1) : d)).ToList();

        var a = ScheduleEngine.Build(new ScheduleInput(P10000, 0.30m, RateType.Efectiva, PaymentFrequency.MENSUAL, 12,
            AmortizationMethod.FRANCES, disbursement, plain)).Value;
        var b = ScheduleEngine.Build(new ScheduleInput(P10000, 0.30m, RateType.Efectiva, PaymentFrequency.MENSUAL, 12,
            AmortizationMethod.FRANCES, disbursement, shifted)).Value;

        b.Lines[0].DueDate.ShouldBe(new DateOnly(2026, 12, 9));
        b.Lines.Select(l => l.Total).ShouldBe(a.Lines.Select(l => l.Total));
    }

    [Fact] // PRO-03.3 periodic fee: separate column, no interest
    public void PeriodicFee_IsAddedToEveryInstallment()
    {
        var s = Build(AmortizationMethod.FRANCES, periodicFee: Money.From(5m));

        s.Lines.ShouldAllBe(l => l.Fees.Amount == 5m);
        s.Lines[0].Total.Amount.ShouldBe(962.86m);
        s.TotalFees.Amount.ShouldBe(60m);
        s.TotalInterest.Amount.ShouldBe(1494.30m);
        s.TotalPayable.Amount.ShouldBe(10000m + 1494.30m + 60m);
    }

    [Theory]
    [InlineData("FRANCES")]
    [InlineData("ALEMAN")]
    [InlineData("AMERICANO")]
    [InlineData("FLAT")]
    public void ZeroRate_SplitsPrincipal(string method)
    {
        var s = Build(Enum.Parse<AmortizationMethod>(method), installments: 3, tea: 0m, principal: Money.From(1000m));

        s.TotalInterest.Amount.ShouldBe(0m);
        AssertBalanced(s, Money.From(1000m));
    }

    [Fact] // CA-02.f: property test with a fixed seed
    public void AnyMethodFrequencyAndTerm_AlwaysBalancesToTheCentavo()
    {
        var random = new Random(20261114);
        int[] terms = [1, 2, 12, 36, 360];

        foreach (var method in Enum.GetValues<AmortizationMethod>())
        foreach (var frequency in Enum.GetValues<PaymentFrequency>())
        foreach (var n in terms)
        {
            for (var sample = 0; sample < 5; sample++)
            {
                var principal = Money.From(100m + random.Next(0, 99_990_000) / 100m);
                var tea = random.Next(0, 20_000) / 10_000m; // 0 % .. 200 %

                var result = ScheduleEngine.Build(new ScheduleInput(
                    principal, tea, RateType.Efectiva, frequency, n, method));

                result.IsSuccess.ShouldBeTrue($"{method} {frequency} n={n} P={principal.Amount} TEA={tea}: {(result.IsFailure ? result.Error.Description : "")}");
                AssertBalanced(result.Value, principal);
            }
        }
    }

    [Fact]
    public void EnsureBalanced_DetectsAnUnbalancedSchedule()
    {
        var pen = Money.Zero();
        var lines = new List<ScheduleLine>
        {
            new(1, null, null, 30, Money.From(500m), pen, pen, Money.From(500m), Money.From(499.99m)),
        };

        var result = ScheduleEngine.EnsureBalanced(Money.From(999.99m), lines);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.ScheduleNotBalanced");
    }

    [Fact] // PRO-02.6: a first period so long that the level installment does not cover its interest
    public void French_Actual360_TooLongFirstPeriod_IsNotAmortizing()
    {
        var disbursement = new DateOnly(2026, 1, 1);
        var dueDates = new List<DueDatePair>
        {
            new(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 1)),
            new(new DateOnly(2030, 1, 2), new DateOnly(2030, 1, 2)),
        };

        var result = ScheduleEngine.Build(new ScheduleInput(
            P10000, 2m, RateType.Efectiva, PaymentFrequency.MENSUAL, 2, AmortizationMethod.FRANCES, disbursement, dueDates));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.ScheduleNotAmortizing");
    }

    [Theory]
    [InlineData(0, 12, 0.3, "Products.InvalidAmount")]
    [InlineData(1000, 0, 0.3, "Products.InvalidTerm")]
    [InlineData(1000, 12, -0.01, "Products.InvalidRate")]
    public void InvalidInput_ReturnsValidationError(decimal amount, int n, decimal tea, string code)
    {
        var result = ScheduleEngine.Build(new ScheduleInput(
            Money.From(amount), tea, RateType.Efectiva, PaymentFrequency.MENSUAL, n, AmortizationMethod.FRANCES));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Actual360_WithoutDueDates_IsRejected()
    {
        var result = ScheduleEngine.Build(new ScheduleInput(
            P10000, 0.3m, RateType.Efectiva, PaymentFrequency.MENSUAL, 2, AmortizationMethod.FRANCES,
            DisbursementDate: new DateOnly(2026, 11, 14), DueDates: [new(new DateOnly(2026, 12, 14), new DateOnly(2026, 12, 14))]));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.InvalidDueDates");
    }

    [Fact]
    public void NegativePeriodicFee_IsRejected()
    {
        var result = ScheduleEngine.Build(new ScheduleInput(
            P10000, 0.3m, RateType.Efectiva, PaymentFrequency.MENSUAL, 12, AmortizationMethod.FRANCES,
            PeriodicFee: Money.From(-1m)));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.InvalidPeriodicFee");
    }

    [Fact] // PRO-02.9: generous CI threshold (target < 5 ms)
    public void Performance_360InstallmentsActual360_IsFast()
    {
        var disbursement = new DateOnly(2026, 11, 14);
        var dueDates = Enumerable.Range(0, 360)
            .Select(k => new DateOnly(2026, 12, 8).AddMonths(k))
            .Select(d => new DueDatePair(d, d))
            .ToList();
        // TEA 12 %: at 30 % a 360-month level installment does not cover a 31-day month (ScheduleNotAmortizing).
        var input = new ScheduleInput(P10000, 0.12m, RateType.Efectiva, PaymentFrequency.MENSUAL, 360,
            AmortizationMethod.FRANCES, disbursement, dueDates);

        ScheduleEngine.Build(input); // warm-up (JIT)
        var stopwatch = Stopwatch.StartNew();
        var result = ScheduleEngine.Build(input);
        stopwatch.Stop();

        result.IsSuccess.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
    }

    private static void AssertBalanced(ScheduleResult s, Money principal)
    {
        s.Lines.Sum(l => l.Principal.Amount).ShouldBe(principal.Amount);
        s.Lines[^1].RemainingBalance.Amount.ShouldBe(0m);
        s.Lines.ShouldAllBe(l => l.Principal.Amount >= 0m && l.Interest.Amount >= 0m);
        s.Lines.ShouldAllBe(l => l.Total.Amount == l.Principal.Amount + l.Interest.Amount + l.Fees.Amount);
        s.TotalInterest.Amount.ShouldBe(s.Lines.Sum(l => l.Interest.Amount));
        s.TotalPayable.Amount.ShouldBe(s.Lines.Sum(l => l.Total.Amount));
    }
}
