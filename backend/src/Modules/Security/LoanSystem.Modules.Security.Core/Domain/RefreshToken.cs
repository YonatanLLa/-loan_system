using System.Net;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>
/// Rotating, individually revocable refresh token. Maps to the <c>refresh_tokens</c> table (BE-SEC-01).
/// Every login starts a <b>family</b>; each rotation issues the next token of the same family and links
/// the previous one to it (<see cref="ReplacedByTokenId"/>). Presenting a token that was already rotated
/// is a reuse signal and revokes the whole family (see backend/docs/DECISION_02_REFRESH_TOKEN_REUSE_DETECTION.md).
/// </summary>
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

    /// <summary>Session the token belongs to: the id of the token issued at login, inherited on every rotation.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Token issued when this one was rotated; <c>null</c> if it was never rotated (active, logged out, suspended…).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>True when the token was revoked because it was exchanged for a newer one (rotation).</summary>
    public bool WasRotated => ReplacedByTokenId is not null;

    /// <summary>Issues the first token of a new family (login). Only the hash is ever persisted; the raw token stays with the client.</summary>
    public static RefreshToken Issue(
        Guid userId, string tokenHash, IClock clock, TimeSpan lifetime, IPAddress? userIp, string? userAgent)
    {
        var id = Guid.NewGuid();
        return Create(id, familyId: id, userId, tokenHash, clock, lifetime, userIp, userAgent);
    }

    /// <summary>
    /// Rotation: issues the next token of this family and revokes this one, linking it to its replacement.
    /// Only an active token can be rotated.
    /// </summary>
    public RefreshToken Rotate(string newTokenHash, IClock clock, TimeSpan lifetime, IPAddress? userIp, string? userAgent)
    {
        if (!IsActive(clock))
            throw new InvalidOperationException("Only an active refresh token can be rotated.");

        var next = Create(Guid.NewGuid(), FamilyId, UserId, newTokenHash, clock, lifetime, userIp, userAgent);
        RevokedAt = clock.UtcNow;
        ReplacedByTokenId = next.Id;
        return next;
    }

    public bool IsExpired(IClock clock) => ExpiresAt <= clock.UtcNow;

    public bool IsActive(IClock clock) => !IsRevoked && !IsExpired(clock);

    public void Revoke(IClock clock)
    {
        if (RevokedAt is null)
        {
            RevokedAt = clock.UtcNow;
        }
    }

    private static RefreshToken Create(
        Guid id, Guid familyId, Guid userId, string tokenHash, IClock clock, TimeSpan lifetime, IPAddress? userIp, string? userAgent) =>
        new()
        {
            Id = id,
            FamilyId = familyId,
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.Add(lifetime),
            UserIp = userIp,
            UserAgent = userAgent
        };
}
