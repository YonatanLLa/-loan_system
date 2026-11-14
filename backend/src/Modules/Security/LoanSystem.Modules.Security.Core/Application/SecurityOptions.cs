using LoanSystem.Modules.Security.Core.Domain;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>Strongly-typed configuration options for the Security module (BE-SEC-01..03).</summary>
internal sealed class SecurityOptions
{
    public int RefreshTokenDays { get; init; } = 14;
    public int MaxFailedAttempts { get; init; } = 5;
    public int LockWindowMinutes { get; init; } = 15;
    public MfaOptions Mfa { get; init; } = new();
}

internal sealed class MfaOptions
{
    public IReadOnlySet<SecurityRole> RequiredRoles { get; init; } =
        new HashSet<SecurityRole> { SecurityRole.ROLE_ADMIN, SecurityRole.ROLE_OPERATOR };

    public string Issuer { get; init; } = "LoanSystem";
    public int ChallengeMinutes { get; init; } = 5;
    public int EnrollmentChallengeMinutes { get; init; } = 10;
    public int MaxAttemptsPerChallenge { get; init; } = 5;
    public int AllowedDriftSteps { get; init; } = 1;
}
