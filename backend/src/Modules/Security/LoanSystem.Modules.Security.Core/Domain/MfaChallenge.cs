using System.Net;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Short-lived, single-use challenge for MFA verification or enrollment. Maps to <c>mfa_challenges</c> (BE-SEC-03).</summary>
internal sealed class MfaChallenge : Entity<Guid>
{
    private MfaChallenge() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public MfaChallengePurpose Purpose { get; private set; }
    public short Attempts { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public IPAddress? UserIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Issues a new MFA challenge. Only the token hash is persisted.</summary>
    public static MfaChallenge Issue(
        Guid userId,
        string tokenHash,
        MfaChallengePurpose purpose,
        IClock clock,
        TimeSpan lifetime,
        IPAddress? userIp,
        string? userAgent) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            Purpose = purpose,
            Attempts = 0,
            ExpiresAt = clock.UtcNow.Add(lifetime),
            UserIp = userIp,
            UserAgent = userAgent,
            CreatedAt = clock.UtcNow
        };

    public bool IsActive(IClock clock) => ConsumedAt is null && ExpiresAt > clock.UtcNow;

    /// <summary>
    /// Increments the failed-attempt counter. Returns <see langword="true"/> if the challenge
    /// has reached <paramref name="maxAttempts"/> and is now exhausted.
    /// </summary>
    public bool RegisterFailedAttempt(int maxAttempts, IClock clock)
    {
        Attempts++;
        if (Attempts >= maxAttempts)
        {
            Consume(clock);
            return true;
        }

        return false;
    }

    public void Consume(IClock clock)
    {
        if (ConsumedAt is null)
        {
            ConsumedAt = clock.UtcNow;
        }
    }
}
