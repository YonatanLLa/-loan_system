using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>System user (backoffice or customer portal). Maps to the <c>users</c> table.</summary>
internal sealed class User : AggregateRoot<Guid>
{
    private User() { }

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public SecurityRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public short FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public bool MfaEnabled { get; private set; }
    public string? MfaSecretEncrypted { get; private set; }
    public DateTimeOffset? MfaEnrolledAt { get; private set; }
    public long? MfaLastUsedStep { get; private set; }

    public bool IsActive => Status == UserStatus.ACTIVE;

    /// <summary>Returns <see langword="true"/> while a temporary lockout window is still in effect (BE-SEC-02).</summary>
    public bool IsLockedOut(IClock clock) =>
        LockedUntil.HasValue && LockedUntil.Value > clock.UtcNow;

    /// <summary>
    /// Increments the failed-login counter and locks the account when <paramref name="maxAttempts"/> is reached
    /// within the lockout window (BE-SEC-02). If a previous lockout has already expired, resets the counter first.
    /// Returns <see langword="true"/> if the account is now locked.
    /// </summary>
    public bool RegisterFailedLogin(IClock clock, int maxAttempts, TimeSpan lockWindow)
    {
        if (Status == UserStatus.LOCKED && (!LockedUntil.HasValue || LockedUntil.Value <= clock.UtcNow))
        {
            ResetFailedLogins();
        }

        FailedLoginCount++;

        if (FailedLoginCount >= maxAttempts)
        {
            Status = UserStatus.LOCKED;
            LockedUntil = clock.UtcNow.Add(lockWindow);
            return true;
        }

        return false;
    }

    /// <summary>Clears any temporary lockout and resets the failed-attempt counter to zero (BE-SEC-02).</summary>
    public void ResetFailedLogins()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        if (Status == UserStatus.LOCKED)
        {
            Status = UserStatus.ACTIVE;
        }
    }

    /// <summary>Marks a successful authentication: clears the failed-attempt counter and stamps the login time (BE-SEC-01, BE-SEC-02).</summary>
    public void RecordSuccessfulLogin(IClock clock)
    {
        ResetFailedLogins();
        LastLoginAt = clock.UtcNow;
    }

    /// <summary>Suspends the user account, preventing further authentication and revoking active sessions (BE-SEC-06).</summary>
    public void Suspend() => Status = UserStatus.INACTIVE;

    /// <summary>Reactivates a suspended user account and resets lockout counters (BE-SEC-06).</summary>
    public void Activate()
    {
        ResetFailedLogins();
        Status = UserStatus.ACTIVE;
    }

    /// <summary>Returns <see langword="true"/> when the user's role requires MFA (BE-SEC-03).</summary>
    public bool RequiresMfa(IReadOnlySet<SecurityRole> requiredRoles) =>
        requiredRoles.Contains(Role);

    /// <summary>Stores a pending encrypted TOTP secret prior to confirmation (BE-SEC-03).</summary>
    public void StartMfaEnrollment(string encryptedSecret)
    {
        if (MfaEnabled)
        {
            throw new InvalidOperationException("MFA is already enabled for this user.");
        }

        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new ArgumentException("Encrypted MFA secret cannot be empty.", nameof(encryptedSecret));
        }

        MfaSecretEncrypted = encryptedSecret;
    }

    /// <summary>Updates the encrypted TOTP secret during lazy key rotation without altering enrollment state (BE-SEC-10).</summary>
    public void ReprotectMfaSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
        {
            throw new ArgumentException("Encrypted MFA secret cannot be empty.", nameof(encryptedSecret));
        }

        MfaSecretEncrypted = encryptedSecret;
    }

    /// <summary>Activates MFA once the user confirms a valid TOTP code and records the used step (BE-SEC-03).</summary>
    public void CompleteMfaEnrollment(IClock clock, long step)
    {
        if (string.IsNullOrWhiteSpace(MfaSecretEncrypted))
        {
            throw new InvalidOperationException("Cannot complete MFA enrollment without a pending secret.");
        }

        MfaEnabled = true;
        MfaEnrolledAt = clock.UtcNow;
        MfaLastUsedStep = step;
    }

    /// <summary>
    /// Rejects TOTP steps equal to or earlier than the last accepted step to prevent replay attacks (BE-SEC-03).
    /// </summary>
    public bool TryAcceptMfaStep(long step)
    {
        if (MfaLastUsedStep.HasValue && step <= MfaLastUsedStep.Value)
        {
            return false;
        }

        MfaLastUsedStep = step;
        return true;
    }

    /// <summary>Clears MFA state so the user must re-enroll on next login (BE-SEC-03).</summary>
    public void ResetMfa()
    {
        MfaEnabled = false;
        MfaSecretEncrypted = null;
        MfaEnrolledAt = null;
        MfaLastUsedStep = null;
    }

    /// <summary>Updates the user's RBAC role upon four-eyes approval (BE-SEC-09).</summary>
    public void ChangeRole(SecurityRole newRole) => Role = newRole;

    /// <summary>Reconstitutes a user from known state. Used by tests and future seeding paths.</summary>
    internal static User Reconstitute(
        Guid id, string email, string passwordHash, string fullName, SecurityRole role, UserStatus status) =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = role,
            Status = status
        };
}
