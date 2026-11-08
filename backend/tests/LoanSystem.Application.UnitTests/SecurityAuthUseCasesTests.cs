using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class SecurityAuthUseCasesTests
{
    private const string Email = "analyst@loan.test";
    private const string Password = "S3cret-Password";

    private readonly FakeClock _clock = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeJwtTokenGenerator _jwt = new();
    private readonly FakeClientInfoProvider _clientInfo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new();

    private User AddActiveUser()
    {
        var user = User.Reconstitute(
            Guid.NewGuid(), Email, _hasher.Hash(Password), "Ana Lista", SecurityRole.ROLE_ANALYST, UserStatus.ACTIVE);
        _users.Add(user);
        return user;
    }

    private LoginUseCase CreateLogin() =>
        new(_users, _refreshTokens, _hasher, _jwt, _clientInfo, _unitOfWork, _clock, refreshTokenDays: 14);

    private RefreshTokenUseCase CreateRefresh() =>
        new(_refreshTokens, _users, _jwt, _clientInfo, _unitOfWork, _clock, refreshTokenDays: 14);

    private LogoutUseCase CreateLogout() =>
        new(_refreshTokens, _currentUser, _unitOfWork, _clock);

    private GetCurrentUserUseCase CreateGetMe() =>
        new(_currentUser, _users);

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndPersists()
    {
        var user = AddActiveUser();

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.Role.ShouldBe("ROLE_ANALYST");
        result.Value.ExpiresInSeconds.ShouldBeGreaterThan(0);

        _refreshTokens.Added.Count.ShouldBe(1);
        _refreshTokens.Added[0].UserId.ShouldBe(user.Id);
        user.LastLoginAt.ShouldBe(_clock.UtcNow);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Login_StoresOnlyTheHashOfTheRefreshToken()
    {
        AddActiveUser();

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        var stored = _refreshTokens.Added[0];
        stored.TokenHash.ShouldNotBe(result.Value.RefreshToken);
        stored.TokenHash.ShouldBe(RefreshTokenHasher.Hash(result.Value.RefreshToken));
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        AddActiveUser();

        var result = await CreateLogin().ExecuteAsync(new LoginRequest("  Analyst@Loan.TEST  ", Password));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedAndPersistsFailedAttempt()
    {
        var user = AddActiveUser();

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, "wrong"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Security.InvalidCredentials");
        user.FailedLoginCount.ShouldBe((short)1);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
        _refreshTokens.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccountAndReturnsLockedError()
    {
        var user = AddActiveUser();
        var useCase = CreateLogin();

        for (var i = 1; i <= 4; i++)
        {
            var attempt = await useCase.ExecuteAsync(new LoginRequest(Email, "wrong"));
            attempt.Error.Type.ShouldBe(ErrorType.Unauthorized);
        }

        var fifthAttempt = await useCase.ExecuteAsync(new LoginRequest(Email, "wrong"));

        fifthAttempt.IsFailure.ShouldBeTrue();
        fifthAttempt.Error.Type.ShouldBe(ErrorType.Locked);
        fifthAttempt.Error.Code.ShouldBe("Security.AccountLocked");
        user.Status.ShouldBe(UserStatus.LOCKED);
        user.FailedLoginCount.ShouldBe((short)5);
        user.LockedUntil.ShouldBe(_clock.UtcNow.AddMinutes(15));
        _unitOfWork.SaveChangesCalls.ShouldBe(5);

        // Even with the right password, while the lockout window is active it must stay 423 Locked.
        var whileLocked = await useCase.ExecuteAsync(new LoginRequest(Email, Password));
        whileLocked.IsFailure.ShouldBeTrue();
        whileLocked.Error.Type.ShouldBe(ErrorType.Locked);
    }

    [Fact]
    public async Task Login_AfterLockoutWindowExpires_AllowsValidLoginAndResetsState()
    {
        var user = AddActiveUser();
        var useCase = CreateLogin();

        for (var i = 0; i < 5; i++)
        {
            await useCase.ExecuteAsync(new LoginRequest(Email, "wrong"));
        }

        _clock.Advance(TimeSpan.FromMinutes(16));

        var result = await useCase.ExecuteAsync(new LoginRequest(Email, Password));

        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.ACTIVE);
        user.FailedLoginCount.ShouldBe((short)0);
        user.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsForbidden()
    {
        _users.Add(User.Reconstitute(
            Guid.NewGuid(), Email, _hasher.Hash(Password), "Ana Lista", SecurityRole.ROLE_ANALYST, UserStatus.INACTIVE));

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("a@b.c", "")]
    public async Task Login_WithMissingCredentials_ReturnsValidation(string email, string password)
    {
        var result = await CreateLogin().ExecuteAsync(new LoginRequest(email, password));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Refresh_WithActiveToken_RotatesAndRevokesTheOldOne()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        var existing = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(existing);

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsSuccess.ShouldBeTrue();
        existing.IsActive(_clock).ShouldBeFalse();          // old token revoked (rotation)
        existing.RevokedAt.ShouldNotBeNull();
        _refreshTokens.Added.Count.ShouldBe(1);             // a brand-new token was issued
        _refreshTokens.Added[0].TokenHash.ShouldNotBe(hash);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
    {
        AddActiveUser();

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest("not-a-real-token"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WhenTokenExpired_ReturnsUnauthorized()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        // Issued in the past with a lifetime already elapsed.
        var pastClock = new FakeClock(_clock.UtcNow.AddDays(-30));
        var expired = RefreshToken.Issue(user.Id, hash, pastClock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(expired);

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_RevokesTokenAndSaves()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        var existing = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(existing);

        var result = await CreateLogout().ExecuteAsync(new LogoutRequest(raw));

        result.IsSuccess.ShouldBeTrue();
        existing.IsActive(_clock).ShouldBeFalse();
        existing.RevokedAt.ShouldBe(_clock.UtcNow);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_WithoutRefreshToken_RevokesAllUserSessionsWhenAuthenticated()
    {
        var user = AddActiveUser();
        _currentUser.Authenticate(user);
        var (_, hash1) = RefreshTokenHasher.Create();
        var (_, hash2) = RefreshTokenHasher.Create();
        var token1 = RefreshToken.Issue(user.Id, hash1, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        var token2 = RefreshToken.Issue(user.Id, hash2, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(token1);
        _refreshTokens.Seed(token2);

        var result = await CreateLogout().ExecuteAsync(new LogoutRequest());

        result.IsSuccess.ShouldBeTrue();
        token1.IsActive(_clock).ShouldBeFalse();
        token2.IsActive(_clock).ShouldBeFalse();
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsProfileAndEffectiveRolePermissions()
    {
        var user = AddActiveUser();
        _currentUser.Authenticate(user);

        var result = await CreateGetMe().ExecuteAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.UserId.ShouldBe(user.Id);
        result.Value.FullName.ShouldBe("Ana Lista");
        result.Value.Email.ShouldBe(Email);
        result.Value.Role.ShouldBe("ROLE_ANALYST");
        result.Value.Permissions.ShouldContain("dashboard.view");
        result.Value.Permissions.ShouldContain("applications.read");
        result.Value.Permissions.ShouldNotContain("admin.manage");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var result = await CreateGetMe().ExecuteAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    // ----- Fakes -------------------------------------------------------------

    private sealed class FakeClock(DateTimeOffset? now = null) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];
        public void Add(User user) => _users.Add(user);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _seeded = [];
        public List<RefreshToken> Added { get; } = [];
        public void Seed(RefreshToken token) => _seeded.Add(token);
        public void Add(RefreshToken token) => Added.Add(token);
        public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(_seeded.FirstOrDefault(t => t.TokenHash == tokenHash && t.RevokedAt is null));
        public Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
        {
            foreach (var token in _seeded.Concat(Added).Where(t => t.UserId == userId && t.RevokedAt is null))
            {
                token.Revoke(clock);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; private set; }
        public string? Email { get; private set; }
        public IReadOnlyCollection<string> Roles { get; private set; } = [];
        public Guid? CustomerId => null;
        public bool IsAuthenticated { get; private set; }
        public bool IsInRole(string role) => Roles.Contains(role);

        public void Authenticate(User user)
        {
            UserId = user.Id;
            Email = user.Email;
            Roles = [user.Role.ToString()];
            IsAuthenticated = true;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash::" + password;
        public bool Verify(string password, string passwordHash) => passwordHash == "hash::" + password;
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user) =>
            ("access-token-for-" + user.Id, DateTimeOffset.UtcNow.AddMinutes(30));
    }

    private sealed class FakeClientInfoProvider : IClientInfoProvider
    {
        public System.Net.IPAddress? IpAddress => null;
        public string? UserAgent => null;
    }

    private sealed class FakeUnitOfWork : ISecurityUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(0);
        }
        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
