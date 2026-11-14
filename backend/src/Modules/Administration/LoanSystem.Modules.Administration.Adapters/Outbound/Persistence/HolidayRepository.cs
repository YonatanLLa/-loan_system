using System.Data.Common;
using Dapper;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Administration.Core.Domain;
using LoanSystem.Modules.Administration.Core.Ports.Outbound;

namespace LoanSystem.Modules.Administration.Adapters.Outbound.Persistence;

internal sealed class HolidayRepository : IHolidayRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public HolidayRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<HolidayEntry>> ListRelevantAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct) as DbConnection
            ?? throw new InvalidOperationException("Expected DbConnection.");

        var rows = await conn.QueryAsync<HolidayRow>(new CommandDefinition(
            """
            SELECT holiday_date AS HolidayDate,
                   is_recurring AS IsRecurring
            FROM holidays
            WHERE is_recurring
               OR holiday_date BETWEEN @From AND @To
            """,
            new { From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: ct));

        return rows.Select(r => new HolidayEntry(DateOnly.FromDateTime(r.HolidayDate), r.IsRecurring)).ToList();
    }

    private sealed record HolidayRow(DateTime HolidayDate, bool IsRecurring);
}
