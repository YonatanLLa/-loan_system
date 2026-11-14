using System.Net;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests.Customers;

public class OtpCodeTests
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_ChecksScopeAndConsumesSingleUseCode()
    {
        var clock = new FakeClock(BaseUtc);
        var hasher = new StubHasher();
        var accountId = Guid.NewGuid();
        var otp = OtpCode.Issue(
            Guid.NewGuid(),
            OtpPurpose.ACCOUNT_CHANGE,
            OtpChannel.SMS,
            "987654321",
            accountId,
            hasher.Hash("123456"),
            clock,
            TimeSpan.FromMinutes(10),
            maxAttempts: 5,
            IPAddress.Loopback);

        otp.MatchesScope(OtpPurpose.ACCOUNT_CHANGE, accountId).ShouldBeTrue();
        otp.MatchesScope(OtpPurpose.ACCOUNT_CHANGE, Guid.NewGuid()).ShouldBeFalse();
        otp.MatchesScope(OtpPurpose.CONTACT_VERIFICATION, accountId).ShouldBeFalse();

        var (first, locked) = otp.Verify("123456", hasher, clock);
        first.IsSuccess.ShouldBeTrue();
        locked.ShouldBeFalse();
        otp.ConsumedAt.ShouldBe(clock.UtcNow);

        var (replay, _) = otp.Verify("123456", hasher, clock);
        replay.IsFailure.ShouldBeTrue();
        replay.Error.Code.ShouldBe("Customers.OtpAlreadyUsed");
    }

    [Fact]
    public void Verify_LocksAfterMaxFailedAttemptsAndRejectsExpiredCodes()
    {
        var clock = new FakeClock(BaseUtc);
        var hasher = new StubHasher();
        var otp = OtpCode.Issue(
            Guid.NewGuid(),
            OtpPurpose.CONTACT_VERIFICATION,
            OtpChannel.EMAIL,
            "ana@test.pe",
            null,
            hasher.Hash("654321"),
            clock,
            TimeSpan.FromMinutes(10),
            maxAttempts: 3,
            null);

        var (firstFail, firstLocked) = otp.Verify("000000", hasher, clock);
        firstFail.IsFailure.ShouldBeTrue();
        firstFail.Error.Code.ShouldBe("Customers.OtpInvalid");
        firstLocked.ShouldBeFalse();

        var (secondFail, secondLocked) = otp.Verify("111111", hasher, clock);
        secondFail.IsFailure.ShouldBeTrue();
        secondFail.Error.Code.ShouldBe("Customers.OtpInvalid");
        secondLocked.ShouldBeFalse();
        var (lockResult, becameLocked) = otp.Verify("222222", hasher, clock);

        becameLocked.ShouldBeTrue();
        lockResult.IsFailure.ShouldBeTrue();
        lockResult.Error.Code.ShouldBe("Customers.OtpLocked");
        otp.InvalidatedAt.ShouldBe(clock.UtcNow);

        // Even with the right code afterwards, locked stays locked
        otp.Verify("654321", hasher, clock).Result.Error.Code.ShouldBe("Customers.OtpLocked");

        // Fresh OTP past TTL expires
        var fresh = OtpCode.Issue(
            Guid.NewGuid(), OtpPurpose.CONTACT_VERIFICATION, OtpChannel.EMAIL, "ana@test.pe", null,
            hasher.Hash("654321"), clock, TimeSpan.FromMinutes(5), 3, null);
        clock.UtcNow = BaseUtc.AddMinutes(5);
        fresh.Verify("654321", hasher, clock).Result.Error.Code.ShouldBe("Customers.OtpExpired");
    }

    private sealed class StubHasher : IOtpHasher
    {
        public string Hash(string rawCode) => $"H:{rawCode}";
        public bool Verify(string rawCode, string storedHash) => storedHash == $"H:{rawCode}";
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now ?? BaseUtc;
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
