using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class SuspendUserUseCase : ISuspendUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IUserStatusTracker _statusTracker;
    private readonly IAuditContext _audit;
    private readonly ISecurityApprovalRequests? _approvals;

    public SuspendUserUseCase(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ICurrentUser currentUser,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IUserStatusTracker statusTracker,
        IAuditContext audit,
        ISecurityApprovalRequests? approvals = null)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _statusTracker = statusTracker;
        _audit = audit;
        _approvals = approvals;
    }

    public async Task<Result> ExecuteAsync(SuspendUserRequest request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return Error.Validation("Security.InvalidUserId", "A valid user identifier is required.");

        if (_currentUser.UserId == request.UserId)
            return Error.Conflict("Security.CannotSuspendSelf", "You cannot suspend your own user account.");

        var user = await _users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Error.NotFound("Security.UserNotFound", $"User '{request.UserId}' was not found.");

        user.Suspend();
        _audit.SetAction("SUSPEND", "users");

        // RevokeAllForUserAsync is an immediate bulk UPDATE; the transaction keeps it atomic with the
        // suspension and the cancellation of the user's pending approvals. Its row locks also make a
        // concurrent rotation of one of these tokens lose the race.
        await using (var transaction = await _unitOfWork.BeginTransactionAsync(ct))
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, _clock, ct);

            if (_approvals is not null)
            {
                var pendingRequests = await _approvals.FindPendingByRequesterAsync(user.Id, ct);
                if (pendingRequests.Count > 0)
                {
                    _audit.SetAction("CANCEL", "approval_requests");
                    foreach (var pending in pendingRequests)
                    {
                        pending.CancelBySystem(_clock, "Requester account was suspended.");
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        _statusTracker.InvalidateUser(user.Id);

        return Result.Success();
    }
}
