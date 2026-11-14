namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>Pure business-day calendar used by the due date generator (PRO-06.1). No I/O.</summary>
internal interface IBusinessCalendar
{
    bool IsBusinessDay(DateOnly date);
}

/// <summary>
/// Non-business day = a configured weekday (Sunday by default, plan D5; Saturday is a business day for a
/// digital lender) or a holiday of the <c>holidays</c> table (already expanded for recurring ones).
/// </summary>
internal sealed class BusinessCalendar : IBusinessCalendar
{
    public static readonly IReadOnlySet<DayOfWeek> DefaultNonBusinessWeekdays = new HashSet<DayOfWeek> { DayOfWeek.Sunday };

    private readonly IReadOnlySet<DateOnly> _holidays;
    private readonly IReadOnlySet<DayOfWeek> _nonBusinessWeekdays;

    public BusinessCalendar(IReadOnlySet<DateOnly> holidays, IReadOnlySet<DayOfWeek>? nonBusinessWeekdays = null)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        _holidays = holidays;
        _nonBusinessWeekdays = nonBusinessWeekdays ?? DefaultNonBusinessWeekdays;

        if (_nonBusinessWeekdays.Count >= 7)
            throw new ArgumentException("At least one weekday must be a business day.", nameof(nonBusinessWeekdays));
    }

    /// <summary>Only weekdays rule (no holidays).</summary>
    public static BusinessCalendar WithoutHolidays(IReadOnlySet<DayOfWeek>? nonBusinessWeekdays = null) =>
        new(new HashSet<DateOnly>(), nonBusinessWeekdays);

    public bool IsBusinessDay(DateOnly date) =>
        !_nonBusinessWeekdays.Contains(date.DayOfWeek) && !_holidays.Contains(date);
}
