using System.Text.Json;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application.Approvals;

internal sealed class UserRoleChangeApprovalHandler : IApprovalHandler
{
    private readonly ISecurityApprovalRequests _approvals;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IUserStatusTracker _statusTracker;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public UserRoleChangeApprovalHandler(
        ISecurityApprovalRequests approvals,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ISecurityUnitOfWork unitOfWork,
        IUserStatusTracker statusTracker,
        IClock clock,
        IAuditContext audit)
    {
        _approvals = approvals;
        _users = users;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _statusTracker = statusTracker;
        _clock = clock;
        _audit = audit;
    }

    public string OperationType => ApprovalOperationTypes.UserRoleChange;

    public async Task<Result<ApprovalOutcome>> ApproveAsync(ApprovalDecision decision, CancellationToken ct = default)
    {
        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        var request = await _approvals.FindByIdAsync(decision.ApprovalRequestId, ct);
        if (request is null)
        {
            await tx.RollbackAsync(ct);
            return Error.NotFound("Approvals.NotFound", $"Approval request '{decision.ApprovalRequestId}' was not found.");
        }

        if (!TryParseRolePayload(request.PayloadJson, out var fromRole, out var toRole))
        {
            await tx.RollbackAsync(ct);
            return Error.Validation("Approvals.InvalidPayload", "Role change payload is invalid.");
        }

        var targetUser = await _users.FindByIdAsync(request.TargetId, ct);
        if (targetUser is null)
        {
            await tx.RollbackAsync(ct);
            return Error.NotFound("Security.UserNotFound", $"User '{request.TargetId}' was not found.");
        }

        var approveResult = request.Approve(decision.ApproverId, _clock, decision.Comment, beneficiaryId: targetUser.Id);
        if (approveResult.IsFailure)
        {
            await tx.RollbackAsync(ct);
            return approveResult.Error;
        }

        // R5: revalidate target state against snapshot
        if (targetUser.Role != fromRole)
        {
            await tx.RollbackAsync(ct);
            return Error.Conflict(
                "Approvals.TargetChanged",
                "The user's role has changed since the approval request was submitted.");
        }

        targetUser.ChangeRole(toRole);
        await _refreshTokens.RevokeAllForUserAsync(targetUser.Id, _clock, ct);

        _audit.SetApproval(request.Id);
        _audit.SetAction("APPROVE", "approval_requests");
        _audit.SetAction("ROLE_CHANGE", "users");

        var saveResult = await _approvals.SaveApprovalChangesAsync(ct);
        if (saveResult.IsFailure)
        {
            await tx.RollbackAsync(ct);
            return saveResult.Error;
        }

        await tx.CommitAsync(ct);

        // Q11: evict cached auth state so existing JWTs carrying the old role are rejected immediately
        _statusTracker.EvictUser(targetUser.Id);

        return new ApprovalOutcome("users", targetUser.Id.ToString());
    }

    private static bool TryParseRolePayload(string payloadJson, out SecurityRole fromRole, out SecurityRole toRole)
    {
        fromRole = default;
        toRole = default;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("fromRole", out var fromEl)
                || !doc.RootElement.TryGetProperty("toRole", out var toEl))
            {
                return false;
            }

            return Enum.TryParse(fromEl.GetString(), ignoreCase: true, out fromRole)
                && Enum.TryParse(toEl.GetString(), ignoreCase: true, out toRole);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
