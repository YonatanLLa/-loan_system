using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class QueryAuditLogsUseCase : IQueryAuditLogsUseCase
{
    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 200;

    private readonly IAuditLogReader _reader;

    public QueryAuditLogsUseCase(IAuditLogReader reader)
    {
        _reader = reader;
    }

    public async Task<Result<PagedResult<AuditLogItem>>> ExecuteAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        if (query.Page < MinPage)
        {
            return Error.Validation("Audit.InvalidPage", $"Page must be greater than or equal to {MinPage}.");
        }

        if (query.PageSize < MinPageSize || query.PageSize > MaxPageSize)
        {
            return Error.Validation("Audit.InvalidPageSize", $"Page size must be between {MinPageSize} and {MaxPageSize}.");
        }

        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            return Error.Validation("Audit.InvalidDateRange", "'From' date must be on or before 'To' date.");
        }

        var result = await _reader.QueryAsync(query, ct);
        return result;
    }
}
