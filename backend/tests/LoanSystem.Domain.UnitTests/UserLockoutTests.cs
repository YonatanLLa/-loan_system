using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class UserLockoutTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private static User CreateActiveUser() =>
        User.Reconstitute(
            Guid.NewGuid(),
            "analyst@loan.test",
            "hash::secret",
            "Ana Lista",
            SecurityRole.ROLE_ANALYST,
            UserStatus.ACTIVE);

    [Fact]
    public void RegisterFailedLogin_BelowThreshold_IncrementsCountWithoutLocking()
    {
        var user = CreateActiveUser();
        var clock = new FakeClock();

        for (var i = 1; i <= 4; i++)
        {
            var locked = user.RegisterFailedLogin(clock, maxAttempts: 5, Window);
            locked.ShouldBeFalse();
            user.FailedLoginCount.ShouldBe((short)i);
            user.Status.ShouldBe(UserStatus.ACTIVE);
            user.LockedUntil.ShouldBeNull();
            user.IsLockedOut(clock).ShouldBeFalse();
        }
    }

    [Fact]
    public void RegisterFailedLogin_OnFifthAttempt_LocksUserForWindow()
    {
        var user = CreateActiveUser();
        var clock = new FakeClock();

        for (var i = 1; i <= 4; i++)
        {
            user.RegisterFailedLogin(clock, maxAttempts: 5, Window).ShouldBeFalse();
        }

        var lockedOnFifth = user.RegisterFailedLogin(clock, maxAttempts: 5, Window);

        lockedOnFifth.ShouldBeTrue();
        user.FailedLoginCount.ShouldBe((short)5);
        user.Status.ShouldBe(UserStatus.LOCKED);
        user.LockedUntil.ShouldBe(clock.UtcNow.Add(Window));
        user.IsLockedOut(clock).ShouldBeTrue();
    }

    [Fact]
    public void IsLockedOut_WhenLockoutWindowExpires_ReturnsFalse()
    {
        var user = CreateActiveUser();
        var clock = new FakeClock();

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(clock, maxAttempts: 5, Window);
        }

        var afterWindow = new FakeClock(clock.UtcNow.AddMinutes(16));
        user.IsLockedOut(afterWindow).ShouldBeFalse();
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailedLoginsAndUnlocksExpiredLockout()
    {
        var user = CreateActiveUser();
        var clock = new FakeClock();

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(clock, maxAttempts: 5, Window);
        }

        var afterWindow = new FakeClock(clock.UtcNow.AddMinutes(16));
        user.RecordSuccessfulLogin(afterWindow);

        user.FailedLoginCount.ShouldBe((short)0);
        user.LockedUntil.ShouldBeNull();
        user.Status.ShouldBe(UserStatus.ACTIVE);
        user.LastLoginAt.ShouldBe(afterWindow.UtcNow);
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
