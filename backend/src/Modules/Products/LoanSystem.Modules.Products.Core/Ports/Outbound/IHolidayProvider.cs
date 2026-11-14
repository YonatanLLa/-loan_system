namespace LoanSystem.Modules.Products.Core.Ports.Outbound;

/// <summary>
/// Holidays for due date shifting (B4). Implemented with Administration's public <c>IHolidayCalendarQuery</c>;
/// Products never reads the <c>holidays</c> table itself.
/// </summary>
internal interface IHolidayProvider
{
    /// <summary>Holidays in [from, to], inclusive, recurring ones expanded. Weekends are not included.</summary>
    Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
