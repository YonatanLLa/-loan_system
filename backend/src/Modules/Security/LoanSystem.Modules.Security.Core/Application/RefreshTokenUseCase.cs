using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class RefreshTokenUseCase : IRefreshTokenUseCase
{
    private static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Security.InvalidRefreshToken", "The refresh token is invalid, expired or revoked.");

    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IClientInfoProvider _clientInfo;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly TimeSpan _refreshLifetime;

    public RefreshTokenUseCase(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        IJwtTokenGenerator jwt,
        IClientInfoProvider clientInfo,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IAuditContext audit,
        int refreshTokenDays)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _jwt = jwt;
        _clientInfo = clientInfo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _refreshLifetime = TimeSpan.FromDays(refreshTokenDays);
    }

    public async Task<Result<AuthTokensResponse>> ExecuteAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Error.Validation("Security.MissingRefreshToken", "A refresh token is required.");

        var hash = RefreshTokenHasher.Hash(request.RefreshToken);
        var current = await _refreshTokens.FindActiveByHashAsync(hash, ct);
        if (current is null || !current.IsActive(_clock))
            return InvalidRefreshToken;

        var user = await _users.FindByIdAsync(current.UserId, ct);
        if (user is null || !user.IsActive)
            return InvalidRefreshToken;

        // Anonymous request: attribute any audited change to the token's owner (BE-SEC-07)
        _audit.SetActor(user.Id);

        // Rotation: the presented token is revoked and replaced by a brand-new pair.
        current.Revoke(_clock);
        var tokens = TokenIssuer.Issue(user, _jwt, _refreshTokens, _clientInfo, _clock, _refreshLifetime);

        await _unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }
}
