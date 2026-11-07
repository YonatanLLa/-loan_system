namespace LoanSystem.Common.Adapters.Clock;

/// <summary>Business time zone (America/Lima) with Windows and UTC fallbacks.</summary>
public static class LimaTimeZone
{
    public static TimeZoneInfo Instance { get; } = Resolve();

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
