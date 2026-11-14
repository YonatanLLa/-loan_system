using LoanSystem.Modules.Administration.Core.Contracts;
using LoanSystem.Modules.Products.Core.Application;
using LoanSystem.Modules.Products.Core.Ports.Outbound;
using Microsoft.Extensions.Caching.Memory;

namespace LoanSystem.Modules.Products.Adapters.Outbound.Administration;

/// <summary>
/// <see cref="IHolidayProvider"/> on top of Administration's public contract (B4). Holidays are cached per calendar
/// year (Products:Cache:HolidaysMinutes, default 1 h) so consecutive quotes do not hit the database.
/// </summary>
internal sealed class AdministrationHolidayProvider : IHolidayProvider
{
    private readonly IHolidayCalendarQuery _calendar;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public AdministrationHolidayProvider(IHolidayCalendarQuery calendar, IMemoryCache cache, ProductsOptions options)
    {
        _calendar = calendar;
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(options.Cache.HolidaysMinutes);
    }

    public async Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentException("'to' must be on or after 'from'.", nameof(to));

        var result = new HashSet<DateOnly>();
        for (var year = from.Year; year <= to.Year; year++)
        {
            var yearHolidays = await GetYearAsync(year, ct);
            result.UnionWith(yearHolidays.Where(d => d >= from && d <= to));
        }

        return result;
    }

    private async Task<IReadOnlySet<DateOnly>> GetYearAsync(int year, CancellationToken ct)
    {
        var key = $"products:holidays:{year}";
        if (_cache.TryGetValue(key, out IReadOnlySet<DateOnly>? cached) && cached is not null)
            return cached;

        var holidays = await _calendar.GetHolidaysAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct);
        _cache.Set(key, holidays, _ttl);
        return holidays;
    }
}
