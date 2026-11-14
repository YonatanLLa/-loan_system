namespace LoanSystem.Modules.Administration.Core.Domain;

/// <summary>A row of the <c>holidays</c> table.</summary>
internal sealed record HolidayEntry(DateOnly Date, bool IsRecurring);

internal static class HolidayCalendar
{
    /// <summary>
    /// Holidays inside [from, to]. A recurring holiday repeats on the same day and month every year;
    /// a recurring 29-Feb only exists in leap years.
    /// </summary>
    public static IReadOnlySet<DateOnly> Expand(IEnumerable<HolidayEntry> entries, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (to < from)
            throw new ArgumentException("'to' must be on or after 'from'.", nameof(to));

        var result = new HashSet<DateOnly>();
        foreach (var entry in entries)
        {
            if (!entry.IsRecurring)
            {
                if (entry.Date >= from && entry.Date <= to)
                    result.Add(entry.Date);
                continue;
            }

            for (var year = from.Year; year <= to.Year; year++)
            {
                if (entry.Date.Month == 2 && entry.Date.Day == 29 && !DateTime.IsLeapYear(year))
                    continue;

                var date = new DateOnly(year, entry.Date.Month, entry.Date.Day);
                if (date >= from && date <= to)
                    result.Add(date);
            }
        }

        return result;
    }
}
