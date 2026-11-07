using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>Builds and persists a fresh access + refresh token pair for a user (shared by login and refresh).</summary>
internal static class TokenIssuer
{
    public static AuthTokensResponse Issue(
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime)
    {
        var (accessToken, expiresAt) = jwt.Generate(user);

        var (rawRefreshToken, refreshHash) = RefreshTokenHasher.Create();
        refreshTokens.Add(RefreshToken.Issue(
            user.Id, refreshHash, clock, refreshLifetime, clientInfo.IpAddress, clientInfo.UserAgent));

        // Whole-second lifetime without floating point (forbidden in Core by the architecture tests).
        var ticks = (expiresAt - clock.UtcNow).Ticks;
        var expiresInSeconds = ticks > 0 ? (int)(ticks / TimeSpan.TicksPerSecond) : 0;

        return new AuthTokensResponse(accessToken, rawRefreshToken, expiresInSeconds, user.Role.ToString());
    }
}
