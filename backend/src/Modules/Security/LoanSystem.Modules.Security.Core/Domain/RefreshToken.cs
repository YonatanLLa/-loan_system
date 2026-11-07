using System.Net;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Rotating, individually revocable refresh token. Maps to the <c>refresh_tokens</c> table (BE-SEC-01).</summary>
internal sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public IPAddress? UserIp { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>Issues a new refresh token. Only the hash is ever persisted; the raw token stays with the client.</summary>
    public static RefreshToken Issue(
        Guid userId, string tokenHash, IClock clock, TimeSpan lifetime, IPAddress? userIp, string? userAgent) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.Add(lifetime),
            UserIp = userIp,
            UserAgent = userAgent
        };

    public bool IsActive(IClock clock) => RevokedAt is null && ExpiresAt > clock.UtcNow;

    public void Revoke(IClock clock)
    {
        if (RevokedAt is null)
        {
            RevokedAt = clock.UtcNow;
        }
    }
}
