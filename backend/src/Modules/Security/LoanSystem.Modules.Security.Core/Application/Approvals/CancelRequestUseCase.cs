using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class CancelRequestUseCase : ICancelRequestUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityApprovalRequests _approvals;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public CancelRequestUseCase(
        ICurrentUser currentUser,
        ISecurityApprovalRequests approvals,
        IClock clock,
        IAuditContext audit)
    {
        _currentUser = currentUser;
        _approvals = approvals;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<ApprovalDecisionResponse>> ExecuteAsync(
        Guid approvalRequestId, CancellationToken ct = default)
    {
        if (approvalRequestId == Guid.Empty)
            return Error.Validation("Approvals.InvalidId", "A valid approval request identifier is required.");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        var request = await _approvals.FindByIdAsync(approvalRequestId, ct);
        if (request is null)
            return Error.NotFound("Approvals.NotFound", $"Approval request '{approvalRequestId}' was not found.");

        var cancelResult = request.Cancel(userId, _clock);
        if (cancelResult.IsFailure)
            return cancelResult.Error;

        _audit.SetApproval(request.Id);
        _audit.SetAction("CANCEL", "approval_requests");

        var saveResult = await _approvals.SaveApprovalChangesAsync(ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return new ApprovalDecisionResponse(request.Id, nameof(ApprovalStatus.CANCELLED));
    }
}
