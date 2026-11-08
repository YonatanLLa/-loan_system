using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>
/// Outbound read port for audit logs (optimized queries via Dapper/SQL) (BE-SEC-08).
/// </summary>
public interface IAuditLogReader
{
    Task<PagedResult<AuditLogItem>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
