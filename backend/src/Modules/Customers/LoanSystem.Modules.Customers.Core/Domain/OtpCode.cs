using System.Net;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>
/// Single-use, time-bounded, purpose- and reference-bound OTP challenge entity mapped to <c>otp_verifications</c> (BE-CUS-11).
/// Reuses <c>verified_at</c> column for <see cref="ConsumedAt"/>.
/// </summary>
internal sealed class OtpCode : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public OtpPurpose Purpose { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public OtpChannel Channel { get; private set; }
    public string Target { get; private set; } = string.Empty;
    public string OtpHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public short Attempts { get; private set; }
    public short MaxAttempts { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }
    public IPAddress? RequesterIp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OtpCode() { }

    public static OtpCode Issue(
        Guid customerId,
        OtpPurpose purpose,
        OtpChannel channel,
        string target,
        Guid? referenceId,
        string otpHash,
        IClock clock,
        TimeSpan ttl,
        int maxAttempts,
        IPAddress? requesterIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(otpHash);
        ArgumentNullException.ThrowIfNull(clock);
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl));
        }

        if (maxAttempts <= 0 || maxAttempts > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        var now = clock.UtcNow;
        return new OtpCode
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Purpose = purpose,
            ReferenceId = referenceId,
            Channel = channel,
            Target = target,
            OtpHash = otpHash,
            ExpiresAt = now.Add(ttl),
            ConsumedAt = null,
            Attempts = 0,
            MaxAttempts = (short)maxAttempts,
            InvalidatedAt = null,
            RequesterIp = requesterIp,
            CreatedAt = now
        };
    }

    public bool MatchesScope(OtpPurpose expectedPurpose, Guid? expectedReferenceId) =>
        Purpose == expectedPurpose && ReferenceId == expectedReferenceId;

    public (Result Result, bool BecameLocked) Verify(string? rawCode, IOtpHasher hasher, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(clock);

        if (ConsumedAt.HasValue)
        {
            return (CustomerErrors.OtpAlreadyUsed, false);
        }

        if (InvalidatedAt.HasValue)
        {
            return Attempts >= MaxAttempts
                ? (CustomerErrors.OtpLocked, false)
                : (CustomerErrors.OtpInvalid, false);
        }

        if (clock.UtcNow >= ExpiresAt)
        {
            return (CustomerErrors.OtpExpired, false);
        }

        var trimmed = rawCode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed) || !hasher.Verify(trimmed, OtpHash))
        {
            Attempts++;
            if (Attempts >= MaxAttempts)
            {
                InvalidatedAt = clock.UtcNow;
                return (CustomerErrors.OtpLocked, true);
            }

            return (CustomerErrors.OtpInvalid, false);
        }

        ConsumedAt = clock.UtcNow;
        return (Result.Success(), false);
    }

    public void Invalidate(IClock clock)
    {
        if (!InvalidatedAt.HasValue && !ConsumedAt.HasValue)
        {
            InvalidatedAt = clock.UtcNow;
        }
    }
}
