using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class LogoutUseCase : ILogoutUseCase
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ICurrentUser _currentUser;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LogoutUseCase(
        IRefreshTokenRepository refreshTokens,
        ICurrentUser currentUser,
        ISecurityUnitOfWork unitOfWork,
        IClock clock)
    {
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> ExecuteAsync(LogoutRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = RefreshTokenHasher.Hash(request.RefreshToken);
            var token = await _refreshTokens.FindByHashAsync(hash, ct);
            if (token is not null && !token.IsRevoked)
            {
                token.Revoke(_clock);
                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                catch (ConcurrencyConflictException)
                {
                    // Someone else revoked it first (concurrent refresh, logout or suspension): the goal
                    // of logout — this token no longer usable — is already met.
                }
            }

            return Result.Success();
        }

        if (_currentUser.UserId is Guid userId)
        {
            await _refreshTokens.RevokeAllForUserAsync(userId, _clock, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        return Error.Validation("Security.MissingRefreshToken", "A refresh token or an authenticated session is required to log out.");
    }
}
