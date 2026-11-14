using LoanSystem.Common.Core.Application;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <param name="PaymentDay">Fixed day of the month (1–28). Only used with MENSUAL; ignored for other frequencies.</param>
/// <param name="MinDaysToFirstInstallment">With a payment day, minimum days between disbursement and the first due date (Products:Engine).</param>
internal sealed record DueDateRequest(
    DateOnly DisbursementDate,
    PaymentFrequency Frequency,
    int Installments,
    int? PaymentDay,
    HolidayShift HolidayShift,
    int MinDaysToFirstInstallment = DueDateGenerator.DefaultMinDaysToFirstInstallment);

/// <summary>
/// Installment due dates (BE-PRO-06). Pure: the calendar is passed in (plan D14).
/// <list type="table">
/// <item><term>MENSUAL + payment day</term><description>k=1: first date with that day strictly after the disbursement and at least
/// <c>MinDaysToFirstInstallment</c> days away; k&gt;1: same day of the following months.</description></item>
/// <item><term>MENSUAL</term><description>disbursement.AddMonths(k), always from the base date (31-Jan → 28/29-Feb → 31-Mar).</description></item>
/// <item><term>QUINCENAL / SEMANAL</term><description>disbursement + 15·k / 7·k days.</description></item>
/// <item><term>DIARIA</term><description>k-th business day after the disbursement (already a business day, never shifted).</description></item>
/// </list>
/// A theoretical date that is not a business day is shifted to the next (NEXT) or previous (PREV) business day.
/// With PREV, if the result is not after the previous enforceable date (or the disbursement), NEXT is used.
/// Interest is always accrued up to the THEORETICAL date (plan D4), so a shift never changes the installment.
/// </summary>
internal static class DueDateGenerator
{
    public const int DefaultMinDaysToFirstInstallment = 15;
    public const int MinPaymentDay = 1;
    public const int MaxPaymentDay = 28;
    private const int MaxShiftDays = 366;

    public static Result<IReadOnlyList<DueDatePair>> Generate(DueDateRequest request, IBusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(calendar);

        if (request.Installments < 1)
            return Error.Validation("Products.InvalidTerm", "Installments must be at least 1.");

        if (request.MinDaysToFirstInstallment < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "MinDaysToFirstInstallment cannot be negative.");

        var paymentDay = request.Frequency == PaymentFrequency.MENSUAL ? request.PaymentDay : null;
        if (paymentDay is < MinPaymentDay or > MaxPaymentDay)
            return InvalidPaymentDay();

        var disbursement = request.DisbursementDate;
        var dates = new List<DueDatePair>(request.Installments);

        if (request.Frequency == PaymentFrequency.DIARIA)
        {
            var day = disbursement;
            for (var k = 1; k <= request.Installments; k++)
            {
                day = NextBusinessDay(day.AddDays(1), calendar);
                dates.Add(new DueDatePair(day, day));
            }

            return dates;
        }

        var firstWithPaymentDay = paymentDay is { } pd
            ? FirstDateWithPaymentDay(disbursement, pd, request.MinDaysToFirstInstallment)
            : (DateOnly?)null;
        var previousDue = disbursement;

        for (var k = 1; k <= request.Installments; k++)
        {
            var theoretical = request.Frequency switch
            {
                PaymentFrequency.MENSUAL => firstWithPaymentDay?.AddMonths(k - 1) ?? disbursement.AddMonths(k),
                PaymentFrequency.QUINCENAL => disbursement.AddDays(15 * k),
                PaymentFrequency.SEMANAL => disbursement.AddDays(7 * k),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Frequency, "Unsupported payment frequency.")
            };

            var due = Shift(theoretical, request.HolidayShift, previousDue, calendar);
            dates.Add(new DueDatePair(theoretical, due));
            previousDue = due;
        }

        return dates;
    }

    /// <summary>
    /// Last date the calendar must cover for <see cref="Generate"/> (PRO-06.1): the application service loads holidays for
    /// [disbursement, horizon]. Generous on purpose: dates outside the loaded range would be treated as business days.
    /// </summary>
    public static DateOnly CalendarHorizon(DateOnly disbursement, PaymentFrequency frequency, int installments, int nonBusinessWeekdays = 1)
    {
        var businessWeekdays = Math.Max(1, 7 - nonBusinessWeekdays);
        return frequency switch
        {
            PaymentFrequency.MENSUAL => disbursement.AddMonths(installments + 2),
            PaymentFrequency.QUINCENAL => disbursement.AddDays(15 * installments + 31),
            PaymentFrequency.SEMANAL => disbursement.AddDays(7 * installments + 31),
            PaymentFrequency.DIARIA => disbursement.AddDays((installments * 7 + businessWeekdays - 1) / businessWeekdays + 60),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported payment frequency.")
        };
    }

    /// <summary>
    /// PRO-06.5 product rules: payment day in 1–28 and only when MENSUAL is an allowed frequency.
    /// </summary>
    public static Error? ValidateProductSettings(int? paymentDay, IReadOnlyCollection<PaymentFrequency> allowedFrequencies)
    {
        ArgumentNullException.ThrowIfNull(allowedFrequencies);

        if (paymentDay is null)
            return null;

        if (paymentDay is < MinPaymentDay or > MaxPaymentDay)
            return InvalidPaymentDay();

        if (!allowedFrequencies.Contains(PaymentFrequency.MENSUAL))
            return Error.Validation(
                "Products.PaymentDayRequiresMonthly",
                "A fixed payment day can only be set when MENSUAL is an allowed frequency.");

        return null;
    }

    private static DateOnly FirstDateWithPaymentDay(DateOnly disbursement, int paymentDay, int minDays)
    {
        var candidate = new DateOnly(disbursement.Year, disbursement.Month, paymentDay);
        while (candidate <= disbursement || candidate.DayNumber - disbursement.DayNumber < minDays)
            candidate = candidate.AddMonths(1);
        return candidate;
    }

    private static DateOnly Shift(DateOnly theoretical, HolidayShift shift, DateOnly previousDue, IBusinessCalendar calendar)
    {
        if (calendar.IsBusinessDay(theoretical))
            return theoretical;

        if (shift == HolidayShift.PREV)
        {
            var previous = PreviousBusinessDay(theoretical.AddDays(-1), calendar);
            if (previous > previousDue)
                return previous;
        }

        return NextBusinessDay(theoretical.AddDays(1), calendar);
    }

    private static DateOnly NextBusinessDay(DateOnly from, IBusinessCalendar calendar)
    {
        for (var i = 0; i < MaxShiftDays; i++)
        {
            var day = from.AddDays(i);
            if (calendar.IsBusinessDay(day))
                return day;
        }

        throw new InvalidOperationException($"No business day found within {MaxShiftDays} days after {from}.");
    }

    private static DateOnly PreviousBusinessDay(DateOnly from, IBusinessCalendar calendar)
    {
        for (var i = 0; i < MaxShiftDays; i++)
        {
            var day = from.AddDays(-i);
            if (calendar.IsBusinessDay(day))
                return day;
        }

        throw new InvalidOperationException($"No business day found within {MaxShiftDays} days before {from}.");
    }

    private static Error InvalidPaymentDay() =>
        Error.Validation(
            "Products.InvalidPaymentDay",
            $"Payment day must be between {MinPaymentDay} and {MaxPaymentDay}.");
}
