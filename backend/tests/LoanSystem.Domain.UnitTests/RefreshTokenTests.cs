using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Domain;
using Shouldly;
using Xunit;

namespace LoanSystem.Domain.UnitTests;

public class RefreshTokenTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    private readonly FakeClock _clock = new();

    private RefreshToken IssueNew() =>
        RefreshToken.Issue(Guid.NewGuid(), "hash-1", _clock, Lifetime, userIp: null, userAgent: null);

    [Fact]
    public void Issue_StartsANewFamilyIdentifiedByItsOwnId()
    {
        var token = IssueNew();

        token.FamilyId.ShouldBe(token.Id);
        token.ReplacedByTokenId.ShouldBeNull();
        token.IsRevoked.ShouldBeFalse();
        token.WasRotated.ShouldBeFalse();
        token.IsActive(_clock).ShouldBeTrue();
    }

    [Fact]
    public void Rotate_IssuesNextTokenInSameFamilyAndRevokesAndLinksTheCurrentOne()
    {
        var current = IssueNew();

        var next = current.Rotate("hash-2", _clock, Lifetime, userIp: null, userAgent: "tests");

        next.Id.ShouldNotBe(current.Id);
        next.FamilyId.ShouldBe(current.FamilyId);
        next.UserId.ShouldBe(current.UserId);
        next.TokenHash.ShouldBe("hash-2");
        next.ExpiresAt.ShouldBe(_clock.UtcNow.Add(Lifetime));
        next.IsActive(_clock).ShouldBeTrue();

        current.RevokedAt.ShouldBe(_clock.UtcNow);
        current.ReplacedByTokenId.ShouldBe(next.Id);
        current.WasRotated.ShouldBeTrue();
        current.IsActive(_clock).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_OfARevokedToken_Throws()
    {
        var token = IssueNew();
        token.Revoke(_clock);

        Should.Throw<InvalidOperationException>(() => token.Rotate("hash-2", _clock, Lifetime, null, null));
        token.ReplacedByTokenId.ShouldBeNull();
    }

    [Fact]
    public void Rotate_OfAnExpiredToken_Throws()
    {
        var token = IssueNew();
        var later = new FakeClock(_clock.UtcNow.Add(Lifetime));

        token.IsExpired(later).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => token.Rotate("hash-2", later, Lifetime, null, null));
    }

    [Fact]
    public void Revoke_WithoutRotation_DoesNotLinkAReplacement()
    {
        var token = IssueNew();

        token.Revoke(_clock);

        token.IsRevoked.ShouldBeTrue();
        token.WasRotated.ShouldBeFalse();
    }

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }
}
