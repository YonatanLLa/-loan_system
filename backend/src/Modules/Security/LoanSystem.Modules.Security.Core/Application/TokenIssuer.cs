using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using IClientInfoProvider = LoanSystem.Modules.Security.Core.Ports.Outbound.IClientInfoProvider;

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
        TimeSpan refreshLifetime,
        Guid? customerId = null) =>
        IssuePair(user, jwt, refreshTokens, clock, customerId, refreshHash => RefreshToken.Issue(
            user.Id, refreshHash, clock, refreshLifetime, clientInfo.IpAddress, clientInfo.UserAgent));

    public static async Task<AuthTokensResponse> IssueAsync(
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime,
        ICustomerPortalLookup? portalLookup,
        CancellationToken ct)
    {
        var customerId = await ResolveCustomerIdAsync(user, portalLookup, ct);
        return Issue(user, jwt, refreshTokens, clientInfo, clock, refreshLifetime, customerId);
    }

    /// <summary>Refresh: <paramref name="current"/> is revoked and replaced by the next token of its family.</summary>
    public static AuthTokensResponse Rotate(
        RefreshToken current,
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime,
        Guid? customerId = null) =>
        IssuePair(user, jwt, refreshTokens, clock, customerId, refreshHash => current.Rotate(
            refreshHash, clock, refreshLifetime, clientInfo.IpAddress, clientInfo.UserAgent));

    public static async Task<AuthTokensResponse> RotateAsync(
        RefreshToken current,
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClientInfoProvider clientInfo,
        IClock clock,
        TimeSpan refreshLifetime,
        ICustomerPortalLookup? portalLookup,
        CancellationToken ct)
    {
        var customerId = await ResolveCustomerIdAsync(user, portalLookup, ct);
        return Rotate(current, user, jwt, refreshTokens, clientInfo, clock, refreshLifetime, customerId);
    }

    private static async Task<Guid?> ResolveCustomerIdAsync(
        User user,
        ICustomerPortalLookup? portalLookup,
        CancellationToken ct)
    {
        if (user.Role != SecurityRole.ROLE_CLIENT || portalLookup is null)
        {
            return null;
        }

        return await portalLookup.FindCustomerIdByPortalUserIdAsync(user.Id, ct);
    }

    private static AuthTokensResponse IssuePair(
        User user,
        IJwtTokenGenerator jwt,
        IRefreshTokenRepository refreshTokens,
        IClock clock,
        Guid? customerId,
        Func<string, RefreshToken> createRefreshToken)
    {
        var (accessToken, expiresAt) = jwt.Generate(user, customerId);

        var (rawRefreshToken, refreshHash) = RefreshTokenHasher.Create();
        refreshTokens.Add(createRefreshToken(refreshHash));

        // Whole-second lifetime without floating point (forbidden in Core by the architecture tests).
        var ticks = (expiresAt - clock.UtcNow).Ticks;
        var expiresInSeconds = ticks > 0 ? (int)(ticks / TimeSpan.TicksPerSecond) : 0;

        return new AuthTokensResponse(accessToken, rawRefreshToken, expiresInSeconds, user.Role.ToString());
    }
}
