namespace LoanSystem.Modules.Administration.Core.Contracts;

/// <summary>
/// Public read-only view of the national holiday calendar (table <c>holidays</c>, US-41 / RF-14.2).
/// Consumed by Products to shift installment due dates (BE-PRO-06). Other modules must never read
/// the <c>holidays</c> table directly.
/// </summary>
public interface IHolidayCalendarQuery
{
    /// <summary>
    /// Holidays between <paramref name="from"/> and <paramref name="to"/> (both inclusive), with recurring holidays
    /// (<c>is_recurring</c>, e.g. Christmas) expanded to every year of the range. Weekends are NOT included:
    /// which weekdays are non-business is each consumer's decision.
    /// </summary>
    Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
