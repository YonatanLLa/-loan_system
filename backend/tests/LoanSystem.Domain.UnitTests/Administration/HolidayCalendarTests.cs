using LoanSystem.Modules.Administration.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Administration;

/// <summary>B4: expansion of the <c>holidays</c> table for Administration's public calendar contract.</summary>
public class HolidayCalendarTests
{
    [Fact]
    public void OneOffHolidays_OnlyInsideTheRange()
    {
        var entries = new[]
        {
            new HolidayEntry(new DateOnly(2026, 12, 8), false),
            new HolidayEntry(new DateOnly(2027, 6, 29), false),
        };

        var result = HolidayCalendar.Expand(entries, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31));

        result.ShouldBe([new DateOnly(2026, 12, 8)]);
    }

    [Fact]
    public void RecurringHolidays_RepeatEveryYearOfTheRange()
    {
        var entries = new[] { new HolidayEntry(new DateOnly(2020, 12, 25), true) };

        var result = HolidayCalendar.Expand(entries, new DateOnly(2026, 12, 1), new DateOnly(2028, 1, 31));

        result.OrderBy(d => d).ShouldBe([new DateOnly(2026, 12, 25), new DateOnly(2027, 12, 25)]);
    }

    [Fact]
    public void RecurringFebruary29_OnlyInLeapYears()
    {
        var entries = new[] { new HolidayEntry(new DateOnly(2024, 2, 29), true) };

        var result = HolidayCalendar.Expand(entries, new DateOnly(2026, 1, 1), new DateOnly(2028, 12, 31));

        result.ShouldBe([new DateOnly(2028, 2, 29)]);
    }

    [Fact]
    public void InvertedRange_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            HolidayCalendar.Expand([], new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
    }
}
