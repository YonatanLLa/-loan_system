using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>Builds and persists a fresh access + refresh token pair for a user (shared by login and refresh).</summary>
internal static class TokenIssuer
{
    /// <summary>Login: the refresh token starts a new family (session).</summary>
    public static AuthTokensResponse Issue(
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime) =>
        IssuePair(user, jwt, refreshTokens, clock, refreshHash => RefreshToken.Issue(
            user.Id, refreshHash, clock, refreshLifetime, clientInfo.IpAddress, clientInfo.UserAgent));

    /// <summary>Refresh: <paramref name="current"/> is revoked and replaced by the next token of its family.</summary>
    public static AuthTokensResponse Rotate(
        RefreshToken current,
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime) =>
        IssuePair(user, jwt, refreshTokens, clock, refreshHash => current.Rotate(
            refreshHash, clock, refreshLifetime, clientInfo.IpAddress, clientInfo.UserAgent));

    private static AuthTokensResponse IssuePair(
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClock clock,
        Func<string, RefreshToken> createRefreshToken)
    {
        var (accessToken, expiresAt) = jwt.Generate(user);

        var (rawRefreshToken, refreshHash) = RefreshTokenHasher.Create();
        refreshTokens.Add(createRefreshToken(refreshHash));

        // Whole-second lifetime without floating point (forbidden in Core by the architecture tests).
        var ticks = (expiresAt - clock.UtcNow).Ticks;
        var expiresInSeconds = ticks > 0 ? (int)(ticks / TimeSpan.TicksPerSecond) : 0;

        return new AuthTokensResponse(accessToken, rawRefreshToken, expiresInSeconds, user.Role.ToString());
    }
}
