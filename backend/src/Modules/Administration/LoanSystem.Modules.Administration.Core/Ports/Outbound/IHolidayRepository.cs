using LoanSystem.Modules.Administration.Core.Domain;

namespace LoanSystem.Modules.Administration.Core.Ports.Outbound;

internal interface IHolidayRepository
{
    /// <summary>Holidays dated inside [from, to] plus every recurring holiday (any year).</summary>
    Task<IReadOnlyList<HolidayEntry>> ListRelevantAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
