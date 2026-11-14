using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Products;

/// <summary>BE-PRO-06 acceptance criteria.</summary>
public class DueDateGeneratorTests
{
    private static readonly IBusinessCalendar NoHolidays = BusinessCalendar.WithoutHolidays();

    private static IBusinessCalendar WithHolidays(params DateOnly[] holidays) =>
        new BusinessCalendar(new HashSet<DateOnly>(holidays));

    private static IReadOnlyList<DueDatePair> Generate(
        DateOnly disbursement,
        PaymentFrequency frequency = PaymentFrequency.MENSUAL,
        int installments = 12,
        int? paymentDay = null,
        HolidayShift shift = HolidayShift.NEXT,
        IBusinessCalendar? calendar = null)
    {
        var result = DueDateGenerator.Generate(
            new DueDateRequest(disbursement, frequency, installments, paymentDay, shift), calendar ?? NoHolidays);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    [Fact] // CA-06.a: the 5th of next month is only 10 days away (< 15) → jump one more month
    public void PaymentDay_TooCloseToDisbursement_MovesToFollowingMonth()
    {
        var dates = Generate(new DateOnly(2026, 11, 25), paymentDay: 5, installments: 3);

        dates[0].TheoreticalDueDate.ShouldBe(new DateOnly(2027, 1, 5));
        (dates[0].TheoreticalDueDate.DayNumber - new DateOnly(2026, 11, 25).DayNumber).ShouldBe(41);
        dates[1].TheoreticalDueDate.ShouldBe(new DateOnly(2027, 2, 5));
        dates[2].TheoreticalDueDate.ShouldBe(new DateOnly(2027, 3, 5));
    }

    [Theory] // CA-06.b: holiday on Tue 2026-12-08
    [InlineData("NEXT", 9)]
    [InlineData("PREV", 7)]
    public void Holiday_ShiftsOnlyTheEnforceableDate(string shift, int expectedDay)
    {
        var dates = Generate(new DateOnly(2026, 11, 14), paymentDay: 8,
            shift: Enum.Parse<HolidayShift>(shift), calendar: WithHolidays(new DateOnly(2026, 12, 8)));

        dates[0].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 12, 8));
        dates[0].DueDate.ShouldBe(new DateOnly(2026, 12, expectedDay));
    }

    [Theory] // CA-06.b: interest of installment 1 is for 24 days with either shift
    [InlineData("NEXT")]
    [InlineData("PREV")]
    public void Holiday_DoesNotChangeTheInterest(string shift)
    {
        var disbursement = new DateOnly(2026, 11, 14);
        var dates = Generate(disbursement, paymentDay: 8,
            shift: Enum.Parse<HolidayShift>(shift), calendar: WithHolidays(new DateOnly(2026, 12, 8)));

        var schedule = Schedule(disbursement, dates);

        schedule.Lines[0].Days.ShouldBe(24);
        schedule.Lines[0].Interest.Amount.ShouldBe(176.45m);
        schedule.InstallmentAmount.Amount.ShouldBe(955.06m);
    }

    [Fact] // CA-06.c / V6: generator + engine end to end
    public void PaymentDay8_Actual360_V6()
    {
        var disbursement = new DateOnly(2026, 11, 14);
        var dates = Generate(disbursement, paymentDay: 8);

        var s = Schedule(disbursement, dates);

        s.InstallmentAmount.Amount.ShouldBe(955.06m);
        s.Lines[0].Interest.Amount.ShouldBe(176.45m);
        s.Lines[1].Days.ShouldBe(31);
        s.Lines[1].Interest.Amount.ShouldBe(210.71m);
        s.Lines[11].TheoreticalDueDate.ShouldBe(new DateOnly(2027, 11, 8));
        s.Lines[11].Total.Amount.ShouldBe(955.02m);
        s.TotalInterest.Amount.ShouldBe(1460.68m);
    }

    [Fact] // CA-06.d: daily, disbursed on Saturday → first installment on Monday (Sunday is not a business day)
    public void Daily_DisbursedOnSaturday_FirstInstallmentOnMonday()
    {
        var saturday = new DateOnly(2026, 11, 14);
        saturday.DayOfWeek.ShouldBe(DayOfWeek.Saturday);

        var dates = Generate(saturday, PaymentFrequency.DIARIA, installments: 7);

        dates[0].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 11, 16));
        dates[0].DueDate.ShouldBe(dates[0].TheoreticalDueDate);
        dates.ShouldAllBe(d => d.DueDate.DayOfWeek != DayOfWeek.Sunday);
        dates[5].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 11, 21)); // Saturday is a business day
        dates[6].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 11, 23)); // skips Sunday 22
    }

    [Fact]
    public void Daily_SkipsHolidays()
    {
        var dates = Generate(new DateOnly(2026, 12, 7), PaymentFrequency.DIARIA, installments: 2,
            calendar: WithHolidays(new DateOnly(2026, 12, 8)));

        dates[0].DueDate.ShouldBe(new DateOnly(2026, 12, 9));
        dates[1].DueDate.ShouldBe(new DateOnly(2026, 12, 10));
    }

    [Fact] // CA-06.e: no carry-over from the 31st (always from the base date)
    public void Monthly_FromJanuary31_DoesNotDrift()
    {
        var dates = Generate(new DateOnly(2027, 1, 31), installments: 3, calendar: new BusinessCalendar(new HashSet<DateOnly>(), new HashSet<DayOfWeek>()));

        dates.Select(d => d.TheoreticalDueDate).ShouldBe(
            [new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30)]);
    }

    [Fact] // leap year
    public void Monthly_FromJanuary31_LeapYear_EndsFebruary29()
    {
        var dates = Generate(new DateOnly(2028, 1, 31), installments: 1, calendar: new BusinessCalendar(new HashSet<DateOnly>(), new HashSet<DayOfWeek>()));

        dates[0].TheoreticalDueDate.ShouldBe(new DateOnly(2028, 2, 29));
    }

    [Theory]
    [InlineData("QUINCENAL", 15)]
    [InlineData("SEMANAL", 7)]
    public void FixedDayFrequencies_AddDays(string frequency, int days)
    {
        var disbursement = new DateOnly(2026, 11, 16); // Monday
        var dates = Generate(disbursement, Enum.Parse<PaymentFrequency>(frequency), installments: 4,
            calendar: new BusinessCalendar(new HashSet<DateOnly>(), new HashSet<DayOfWeek>()));

        dates.Select((d, i) => d.TheoreticalDueDate.DayNumber - disbursement.DayNumber).ShouldBe([days, 2 * days, 3 * days, 4 * days]);
    }

    [Fact] // PRO-06.3: PREV that would fall on/before the previous due date uses NEXT
    public void Prev_ClashingWithPreviousDueDate_FallsBackToNext()
    {
        // Weekly from Mon 2026-11-16: due Mon 23 (business). Holidays Tue 24 .. Sun 29 make Mon 30's PREV land on 23.
        var holidays = Enumerable.Range(24, 7).Select(d => new DateOnly(2026, 11, d)).ToArray(); // 24..30
        var dates = Generate(new DateOnly(2026, 11, 16), PaymentFrequency.SEMANAL, installments: 2,
            shift: HolidayShift.PREV, calendar: WithHolidays(holidays));

        dates[0].DueDate.ShouldBe(new DateOnly(2026, 11, 23));
        dates[1].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 11, 30));
        dates[1].DueDate.ShouldBe(new DateOnly(2026, 12, 1));
    }

    [Fact] // PRO-06.3: PREV never moves a due date onto/before the disbursement
    public void Prev_BeforeDisbursement_FallsBackToNext()
    {
        // Disbursed Sat 2026-11-14, weekly: theoretical Sat 21 is a holiday; previous business days 16..20 are holidays too.
        var holidays = Enumerable.Range(15, 7).Select(d => new DateOnly(2026, 11, d)).ToArray();
        var dates = Generate(new DateOnly(2026, 11, 14), PaymentFrequency.SEMANAL, installments: 1,
            shift: HolidayShift.PREV, calendar: WithHolidays(holidays));

        dates[0].DueDate.ShouldBe(new DateOnly(2026, 11, 23));
    }

    [Fact] // payment day is only used with MENSUAL
    public void PaymentDay_IgnoredForWeekly()
    {
        var dates = Generate(new DateOnly(2026, 11, 16), PaymentFrequency.SEMANAL, installments: 1, paymentDay: 5);

        dates[0].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 11, 23));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    public void InvalidPaymentDay_IsRejected(int paymentDay)
    {
        var result = DueDateGenerator.Generate(
            new DueDateRequest(new DateOnly(2026, 11, 14), PaymentFrequency.MENSUAL, 12, paymentDay, HolidayShift.NEXT), NoHolidays);

        result.Error.Code.ShouldBe("Products.InvalidPaymentDay");
    }

    [Fact] // PRO-06.5
    public void ValidateProductSettings()
    {
        DueDateGenerator.ValidateProductSettings(null, [PaymentFrequency.SEMANAL]).ShouldBeNull();
        DueDateGenerator.ValidateProductSettings(8, [PaymentFrequency.MENSUAL, PaymentFrequency.SEMANAL]).ShouldBeNull();
        DueDateGenerator.ValidateProductSettings(8, [PaymentFrequency.SEMANAL])!.Code.ShouldBe("Products.PaymentDayRequiresMonthly");
        DueDateGenerator.ValidateProductSettings(31, [PaymentFrequency.MENSUAL])!.Code.ShouldBe("Products.InvalidPaymentDay");
    }

    [Theory] // the holiday range loaded by the service always covers the last enforceable date
    [InlineData("MENSUAL", 360, 28)]
    [InlineData("MENSUAL", 12, null)]
    [InlineData("QUINCENAL", 48, null)]
    [InlineData("SEMANAL", 104, null)]
    [InlineData("DIARIA", 720, null)]
    public void CalendarHorizon_CoversTheLastDueDate(string frequency, int installments, int? paymentDay)
    {
        var disbursement = new DateOnly(2026, 11, 30);
        var freq = Enum.Parse<PaymentFrequency>(frequency);

        var dates = Generate(disbursement, freq, installments, paymentDay);

        DueDateGenerator.CalendarHorizon(disbursement, freq, installments).ShouldBeGreaterThanOrEqualTo(dates[^1].DueDate);
    }

    private static ScheduleResult Schedule(DateOnly disbursement, IReadOnlyList<DueDatePair> dates) =>
        ScheduleEngine.Build(new ScheduleInput(
            Money.From(10000m), 0.30m, RateType.Efectiva, PaymentFrequency.MENSUAL, dates.Count, AmortizationMethod.FRANCES,
            disbursement, dates)).Value;
}
