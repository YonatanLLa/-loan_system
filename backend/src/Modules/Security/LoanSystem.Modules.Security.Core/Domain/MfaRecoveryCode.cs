using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Domain;

/// <summary>Single-use MFA recovery code stored as a hash. Maps to <c>mfa_recovery_codes</c> (BE-SEC-03).</summary>
internal sealed class MfaRecoveryCode : Entity<Guid>
{
    private MfaRecoveryCode() { }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsUnused => UsedAt is null;

    public static MfaRecoveryCode Create(Guid userId, string codeHash, IClock clock) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = codeHash,
            CreatedAt = clock.UtcNow
        };

    public void Use(IClock clock)
    {
        if (UsedAt is null)
        {
            UsedAt = clock.UtcNow;
        }
    }
}
