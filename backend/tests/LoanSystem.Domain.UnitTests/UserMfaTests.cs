using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class UserMfaTests
{
    private static User CreateAdminUser() =>
        User.Reconstitute(
            Guid.NewGuid(),
            "admin@loan.test",
            "hash::secret",
            "Admin User",
            SecurityRole.ROLE_ADMIN,
            UserStatus.ACTIVE);

    [Fact]
    public void RequiresMfa_MatchesConfiguredRoles()
    {
        var required = new HashSet<SecurityRole> { SecurityRole.ROLE_ADMIN, SecurityRole.ROLE_OPERATOR };
        var admin = CreateAdminUser();
        var analyst = User.Reconstitute(
            Guid.NewGuid(), "analyst@loan.test", "hash", "Ana", SecurityRole.ROLE_ANALYST, UserStatus.ACTIVE);

        admin.RequiresMfa(required).ShouldBeTrue();
        analyst.RequiresMfa(required).ShouldBeFalse();
    }

    [Fact]
    public void StartAndCompleteMfaEnrollment_EnablesMfaAndRecordsStep()
    {
        var user = CreateAdminUser();
        var clock = new FakeClock();

        user.StartMfaEnrollment("v1:encrypted-secret");
        user.MfaEnabled.ShouldBeFalse();
        user.MfaSecretEncrypted.ShouldBe("v1:encrypted-secret");

        user.CompleteMfaEnrollment(clock, step: 58_000_000L);

        user.MfaEnabled.ShouldBeTrue();
        user.MfaEnrolledAt.ShouldBe(clock.UtcNow);
        user.MfaLastUsedStep.ShouldBe(58_000_000L);
    }

    [Fact]
    public void StartMfaEnrollment_WhenAlreadyEnabled_Throws()
    {
        var user = CreateAdminUser();
        var clock = new FakeClock();
        user.StartMfaEnrollment("v1:secret");
        user.CompleteMfaEnrollment(clock, step: 100);

        Should.Throw<InvalidOperationException>(() => user.StartMfaEnrollment("v1:other"));
    }

    [Fact]
    public void TryAcceptMfaStep_RejectsEqualOrEarlierStep_AcceptsNewerStep()
    {
        var user = CreateAdminUser();
        var clock = new FakeClock();
        user.StartMfaEnrollment("v1:secret");
        user.CompleteMfaEnrollment(clock, step: 100);

        user.TryAcceptMfaStep(100).ShouldBeFalse();
        user.TryAcceptMfaStep(99).ShouldBeFalse();
        user.TryAcceptMfaStep(101).ShouldBeTrue();
        user.MfaLastUsedStep.ShouldBe(101);
    }

    [Fact]
    public void ResetMfa_ClearsAllMfaFields()
    {
        var user = CreateAdminUser();
        var clock = new FakeClock();
        user.StartMfaEnrollment("v1:secret");
        user.CompleteMfaEnrollment(clock, step: 100);

        user.ResetMfa();

        user.MfaEnabled.ShouldBeFalse();
        user.MfaSecretEncrypted.ShouldBeNull();
        user.MfaEnrolledAt.ShouldBeNull();
        user.MfaLastUsedStep.ShouldBeNull();
    }

    [Fact]
    public void MfaChallenge_ExpiresAndConsumesAfterMaxAttempts()
    {
        var clock = new FakeClock();
        var challenge = MfaChallenge.Issue(
            Guid.NewGuid(),
            "hash",
            MfaChallengePurpose.VERIFY,
            clock,
            TimeSpan.FromMinutes(5),
            userIp: null,
            userAgent: "TestAgent");

        challenge.IsActive(clock).ShouldBeTrue();
        challenge.RegisterFailedAttempt(maxAttempts: 3, clock).ShouldBeFalse();
        challenge.RegisterFailedAttempt(maxAttempts: 3, clock).ShouldBeFalse();
        challenge.IsActive(clock).ShouldBeTrue();

        var exhausted = challenge.RegisterFailedAttempt(maxAttempts: 3, clock);
        exhausted.ShouldBeTrue();
        challenge.IsActive(clock).ShouldBeFalse();
        challenge.ConsumedAt.ShouldBe(clock.UtcNow);
    }

    [Fact]
    public void MfaChallenge_WhenExpired_IsNotActive()
    {
        var clock = new FakeClock();
        var challenge = MfaChallenge.Issue(
            Guid.NewGuid(),
            "hash",
            MfaChallengePurpose.ENROLL,
            clock,
            TimeSpan.FromMinutes(10),
            userIp: null,
            userAgent: null);

        var later = new FakeClock(clock.UtcNow.AddMinutes(10));
        challenge.IsActive(later).ShouldBeFalse();
    }

    [Fact]
    public void MfaRecoveryCode_Use_MarksUsedAt()
    {
        var clock = new FakeClock();
        var code = MfaRecoveryCode.Create(Guid.NewGuid(), "code-hash", clock);

        code.IsUnused.ShouldBeTrue();
        code.Use(clock);
        code.IsUnused.ShouldBeFalse();
        code.UsedAt.ShouldBe(clock.UtcNow);
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now ?? new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
