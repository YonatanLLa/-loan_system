using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class ExpireApprovalRequestsUseCase : IExpireApprovalRequestsUseCase
{
    private readonly ISecurityApprovalRequests _approvals;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public ExpireApprovalRequestsUseCase(
        ISecurityApprovalRequests approvals,
        IClock clock,
        IAuditContext audit)
    {
        _approvals = approvals;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<int>> ExecuteAsync(CancellationToken ct = default)
    {
        var expired = await _approvals.FindExpiredPendingAsync(_clock, ct);
        if (expired.Count == 0)
            return 0;

        var count = 0;
        foreach (var request in expired)
        {
            if (request.Expire(_clock).IsSuccess)
            {
                count++;
            }
        }

        if (count == 0)
            return 0;

        _audit.SetAction("EXPIRE", "approval_requests");
        var save = await _approvals.SaveApprovalChangesAsync(ct);
        if (save.IsFailure)
            return save.Error;

        return count;
    }
}
