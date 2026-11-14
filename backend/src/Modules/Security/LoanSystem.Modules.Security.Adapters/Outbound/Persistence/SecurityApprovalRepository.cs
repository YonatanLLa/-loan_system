using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class SecurityApprovalRepository : ISecurityApprovalRequests
{
    private readonly SecurityDbContext _db;

    public SecurityApprovalRepository(SecurityDbContext db) => _db = db;

    public void Add(ApprovalRequest request) => _db.ApprovalRequests.Add(request);

    public Task<ApprovalRequest?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ExistsPendingAsync(string operationType, Guid targetId, CancellationToken ct = default) =>
        _db.ApprovalRequests.AnyAsync(
            a => a.OperationType == operationType
              && a.TargetId == targetId
              && a.Status == ApprovalStatus.PENDING,
            ct);

    public async Task<IReadOnlyList<ApprovalRequest>> FindPendingByRequesterAsync(Guid requesterId, CancellationToken ct = default) =>
        await _db.ApprovalRequests
            .Where(a => a.RequestedBy == requesterId && a.Status == ApprovalStatus.PENDING)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApprovalRequest>> FindExpiredPendingAsync(IClock clock, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return await _db.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.PENDING && a.ExpiresAt <= now)
            .ToListAsync(ct);
    }

    public Task<Result> SaveApprovalChangesAsync(CancellationToken ct = default) =>
        _db.SaveApprovalChangesAsync(ct);
}
