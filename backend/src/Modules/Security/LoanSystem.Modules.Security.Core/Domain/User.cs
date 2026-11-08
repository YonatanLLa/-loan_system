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
