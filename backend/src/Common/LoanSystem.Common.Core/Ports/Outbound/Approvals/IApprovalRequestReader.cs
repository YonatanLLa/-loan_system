using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Contracts.Approvals;

namespace LoanSystem.Common.Core.Ports.Outbound.Approvals;

/// <summary>Cross-module read-only query port for the centralized approvals inbox (BE-SEC-09).</summary>
public interface IApprovalRequestReader
{
    Task<ApprovalRequestHeader?> GetHeaderAsync(Guid id, CancellationToken ct = default);
    Task<ApprovalRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ApprovalRequestSummary>> QueryAsync(ApprovalQuery query, Guid currentUserId, CancellationToken ct = default);
}
