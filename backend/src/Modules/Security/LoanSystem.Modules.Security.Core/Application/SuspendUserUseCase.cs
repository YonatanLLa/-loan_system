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

    public SuspendUserUseCase(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ICurrentUser currentUser,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IUserStatusTracker statusTracker,
        IAuditContext audit)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _statusTracker = statusTracker;
        _audit = audit;
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
        _audit.SetAction("SUSPEND");
        await _refreshTokens.RevokeAllForUserAsync(user.Id, _clock, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _statusTracker.InvalidateUser(user.Id);

        return Result.Success();
    }
}
