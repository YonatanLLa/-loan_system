using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

/// <summary>Shared validation for single-use MFA challenges and target user state (BE-SEC-03).</summary>
internal static class MfaChallengeValidator
{
    public static readonly Error InvalidChallenge =
        Error.Unauthorized("Security.InvalidMfaChallenge", "The MFA challenge is invalid or has expired.");

    public static readonly Error AccountLocked =
        Error.Locked("Security.AccountLocked", "The user account is temporarily locked due to too many failed attempts.");

    public static async Task<Result<(MfaChallenge Challenge, User User)>> ValidateAsync(
        string? rawToken,
        MfaChallengePurpose expectedPurpose,
        IMfaChallengeRepository challenges,
        IUserRepository users,
        IClock clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return InvalidChallenge;

        var tokenHash = RefreshTokenHasher.Hash(rawToken);
        var challenge = await challenges.FindByHashAsync(tokenHash, ct);
        if (challenge is null || challenge.Purpose != expectedPurpose || !challenge.IsActive(clock))
            return InvalidChallenge;

        var user = await users.FindByIdAsync(challenge.UserId, ct);
        if (user is null)
            return InvalidChallenge;

        if (user.Status == UserStatus.INACTIVE)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        if (user.IsLockedOut(clock))
            return AccountLocked;

        return (challenge, user);
    }
}
