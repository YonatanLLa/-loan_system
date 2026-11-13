using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Ports.Outbound;

/// <summary>Security-scoped persistence port for <see cref="ApprovalRequest"/> (BE-SEC-09).</summary>
internal interface ISecurityApprovalRequests
{
    void Add(ApprovalRequest request);
    Task<ApprovalRequest?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsPendingAsync(string operationType, Guid targetId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequest>> FindPendingByRequesterAsync(Guid requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequest>> FindExpiredPendingAsync(IClock clock, CancellationToken ct = default);
    Task<Result> SaveApprovalChangesAsync(CancellationToken ct = default);
}
