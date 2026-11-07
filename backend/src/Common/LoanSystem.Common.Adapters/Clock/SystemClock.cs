using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Common.Adapters.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTime LimaNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LimaTimeZone.Instance);

    public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
}
