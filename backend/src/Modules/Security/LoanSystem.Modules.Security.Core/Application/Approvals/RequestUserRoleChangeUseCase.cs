using System.Text.Json;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class RequestUserRoleChangeUseCase : IRequestUserRoleChangeUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly ISecurityApprovalRequests _approvals;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly ApprovalsOptions _options;

    public RequestUserRoleChangeUseCase(
        ICurrentUser currentUser,
        IUserRepository users,
        ISecurityApprovalRequests approvals,
        IClock clock,
        IAuditContext audit,
        ApprovalsOptions options)
    {
        _currentUser = currentUser;
        _users = users;
        _approvals = approvals;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<ApprovalRequestDto>> ExecuteAsync(
        RequestUserRoleChangeCommand command, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid requesterId)
            return Error.Unauthorized("Security.Unauthenticated", "User is not authenticated.");

        if (command.TargetUserId == Guid.Empty)
            return Error.Validation("Security.InvalidUserId", "A valid user identifier is required.");

        if (string.IsNullOrWhiteSpace(command.NewRole)
            || !Enum.TryParse<SecurityRole>(command.NewRole.Trim(), ignoreCase: true, out var newRole))
        {
            return Error.Validation("Security.InvalidRole", $"Role '{command.NewRole}' is not valid.");
        }

        var requester = await _users.FindByIdAsync(requesterId, ct);
        if (requester is null)
            return Error.Unauthorized("Security.Unauthenticated", "Requester account could not be resolved.");

        if (!requester.IsActive)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        if (!RolePermissions.HasPermission(requester.Role, "admin.manage"))
            return Error.Forbidden("Approvals.NotAllowed", "You do not have permission to request role changes.");

        var targetUser = await _users.FindByIdAsync(command.TargetUserId, ct);
        if (targetUser is null)
            return Error.NotFound("Security.UserNotFound", $"User '{command.TargetUserId}' was not found.");

        if (targetUser.Role == newRole)
            return Error.Validation("Security.SameRole", "The user already has the specified role.");

        if (await _approvals.ExistsPendingAsync(ApprovalOperationTypes.UserRoleChange, targetUser.Id, ct))
        {
            return Error.Conflict(
                "Approvals.DuplicatePending",
                "There is already a pending role change request for this user.");
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            fromRole = targetUser.Role.ToString(),
            toRole = newRole.ToString()
        });

        var snapshotJson = JsonSerializer.Serialize(new
        {
            role = targetUser.Role.ToString(),
            status = targetUser.Status.ToString(),
            email = targetUser.Email,
            fullName = targetUser.FullName
        });

        var submission = new ApprovalSubmission(
            ApprovalOperationTypes.UserRoleChange,
            Module: "Security",
            TargetEntity: "users",
            TargetId: targetUser.Id,
            PayloadJson: payloadJson,
            TargetSnapshotJson: snapshotJson,
            Reason: command.Reason);

        var ttl = _options.GetExpirationFor(ApprovalOperationTypes.UserRoleChange);
        var submitResult = ApprovalRequest.Submit(submission, requester.Id, _clock, ttl);
        if (submitResult.IsFailure)
            return submitResult.Error;

        var approval = submitResult.Value;
        _approvals.Add(approval);
        _audit.SetApproval(approval.Id);
        _audit.SetAction("REQUEST_USER_ROLE_CHANGE", "approval_requests");

        var saveResult = await _approvals.SaveApprovalChangesAsync(ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        using var payloadDoc = JsonDocument.Parse(approval.PayloadJson);
        using var snapshotDoc = JsonDocument.Parse(approval.TargetSnapshotJson);

        return new ApprovalRequestDto(
            approval.Id,
            approval.OperationType,
            approval.TargetEntity,
            approval.TargetId,
            approval.Status.ToString(),
            payloadDoc.RootElement.Clone(),
            snapshotDoc.RootElement.Clone(),
            snapshotDoc.RootElement.Clone(),
            approval.Reason,
            approval.EvidenceRef,
            new ApprovalUserRef(requester.Id, requester.FullName, requester.Email),
            approval.RequestedAt,
            approval.ExpiresAt,
            null,
            null,
            null);
    }
}
