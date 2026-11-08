using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Contracts;

namespace LoanSystem.Modules.Security.Core.Ports.Inbound;

/// <summary>
/// Inbound port for querying the immutable audit log with filters and pagination (BE-SEC-08).
/// </summary>
public interface IQueryAuditLogsUseCase : IUseCase<AuditLogQuery, PagedResult<AuditLogItem>>;
