using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>
/// Refresh-token rotation with reuse detection and atomic rotation
/// (BE-SEC-01, backend/docs/DECISION_02_REFRESH_TOKEN_REUSE_DETECTION.md).
/// </summary>
internal sealed class RefreshTokenUseCase : IRefreshTokenUseCase
{
    private const string GenericMessage = "The refresh token is invalid, expired or revoked.";

    internal static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Security.InvalidRefreshToken", GenericMessage);

    // Same 401 and message as InvalidRefreshToken so the caller learns nothing new; the distinct code lets
    // the legitimate client (and the logs/alerts) tell that the whole session family was revoked.
    internal static readonly Error RefreshTokenReused =
        Error.Unauthorized("Security.RefreshTokenReused", GenericMessage);

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
        var current = await _refreshTokens.FindByHashAsync(hash, ct);
        if (current is null)
            return InvalidRefreshToken;

        if (current.WasRotated)
        {
            // REUSE DETECTED: this token was already exchanged for a newer one, so two parties hold the
            // chain (stolen token). We cannot tell who is legitimate: revoke the whole family.
            _audit.SetActor(current.UserId);
            await _refreshTokens.RevokeFamilyAsync(current.FamilyId, _clock, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return RefreshTokenReused;
        }

        // Revoked without replacement (logout, suspension, family already revoked) or expired.
        if (!current.IsActive(_clock))
            return InvalidRefreshToken;

        var user = await _users.FindByIdAsync(current.UserId, ct);
        if (user is null || !user.IsActive)
            return InvalidRefreshToken;

        // Anonymous request: attribute any audited change to the token's owner (BE-SEC-07)
        _audit.SetActor(user.Id);

        // Rotation: the presented token is revoked (linked to its replacement) and a new pair is issued in
        // the same family. INSERT + conditional UPDATE (revoked_at IS NULL) commit in one SaveChanges.
        var tokens = TokenIssuer.Rotate(current, user, _jwt, _refreshTokens, _clientInfo, _clock, _refreshLifetime);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Lost a race against another refresh (or logout/suspension) with the same token: benign
            // (e.g. two tabs). Do not revoke the family; a real thief is caught on the next reuse,
            // because this token is now revoked and linked to its replacement.
            return InvalidRefreshToken;
        }

        return tokens;
    }
}
