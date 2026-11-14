using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class ResetUserMfaUseCase : IResetUserMfaUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IMfaRecoveryCodeRepository _recoveryCodes;
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public ResetUserMfaUseCase(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IMfaRecoveryCodeRepository recoveryCodes,
        ICurrentUser currentUser,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IAuditContext audit)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _recoveryCodes = recoveryCodes;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result> ExecuteAsync(ResetUserMfaRequest request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return Error.Validation("Security.InvalidUserId", "A valid user identifier is required.");

        if (_currentUser.UserId == request.UserId)
            return Error.Conflict("Security.CannotResetOwnMfa", "You cannot reset your own MFA configuration.");

        var user = await _users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Error.NotFound("Security.UserNotFound", $"User '{request.UserId}' was not found.");

        user.ResetMfa();
        _audit.SetAction("MFA_RESET");
        await _recoveryCodes.MarkAllUsedForUserAsync(user.Id, _clock, ct);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, _clock, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
