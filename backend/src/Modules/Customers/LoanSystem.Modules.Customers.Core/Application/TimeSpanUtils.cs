namespace LoanSystem.Modules.Customers.Core.Application;

/// <summary>Integer-based TimeSpan helpers preventing any float/double IL opcodes in Core.</summary>
internal static class TimeSpanUtils
{
    public static TimeSpan Seconds(long seconds) =>
        TimeSpan.FromTicks(checked(seconds * TimeSpan.TicksPerSecond));

    public static TimeSpan Hours(int hours) =>
        new(hours, 0, 0);

    public static TimeSpan Days(int days) =>
        new(days, 0, 0, 0);
}
