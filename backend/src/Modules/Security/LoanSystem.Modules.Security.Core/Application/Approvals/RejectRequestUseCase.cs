using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class RejectRequestUseCase : IRejectRequestUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly ISecurityApprovalRequests _approvals;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public RejectRequestUseCase(
        ICurrentUser currentUser,
        IUserRepository users,
        ISecurityApprovalRequests approvals,
        IClock clock,
        IAuditContext audit)
    {
        _currentUser = currentUser;
        _users = users;
        _approvals = approvals;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<ApprovalDecisionResponse>> ExecuteAsync(
        RejectApprovalCommand command, CancellationToken ct = default)
    {
        if (command.ApprovalRequestId == Guid.Empty)
            return Error.Validation("Approvals.InvalidId", "A valid approval request identifier is required.");

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid approverId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        var request = await _approvals.FindByIdAsync(command.ApprovalRequestId, ct);
        if (request is null)
            return Error.NotFound("Approvals.NotFound", $"Approval request '{command.ApprovalRequestId}' was not found.");

        var approver = await _users.FindByIdAsync(approverId, ct);
        if (approver is null)
            return Error.Unauthorized("Security.Unauthenticated", "Approver account could not be resolved.");

        if (!approver.IsActive)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        var requiredPermission = ApprovalOperationPolicy.GetApprovalPermission(request.OperationType);
        if (requiredPermission is null || !RolePermissions.HasPermission(approver.Role, requiredPermission))
            return Error.Forbidden("Approvals.NotAllowed", "You do not have permission to reject this operation.");

        var rejectResult = request.Reject(approver.Id, _clock, command.Comment);
        if (rejectResult.IsFailure)
            return rejectResult.Error;

        _audit.SetApproval(request.Id);
        _audit.SetAction("REJECT", "approval_requests");

        var saveResult = await _approvals.SaveApprovalChangesAsync(ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return new ApprovalDecisionResponse(request.Id, nameof(ApprovalStatus.REJECTED));
    }
}
