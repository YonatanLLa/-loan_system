using LoanSystem.Modules.Administration.Core.Contracts;
using LoanSystem.Modules.Administration.Core.Domain;
using LoanSystem.Modules.Administration.Core.Ports.Outbound;

namespace LoanSystem.Modules.Administration.Core.Application;

internal sealed class HolidayCalendarQuery : IHolidayCalendarQuery
{
    private readonly IHolidayRepository _repository;

    public HolidayCalendarQuery(IHolidayRepository repository) => _repository = repository;

    public async Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentException("'to' must be on or after 'from'.", nameof(to));

        var entries = await _repository.ListRelevantAsync(from, to, ct);
        return HolidayCalendar.Expand(entries, from, to);
    }
}
