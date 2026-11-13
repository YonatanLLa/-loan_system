using System.Net;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class SecurityMfaUseCasesTests
{
    private const string AdminEmail = "admin@loan.test";
    private const string Password = "S3cret-Password";
    private const string ValidCode = "123456";

    private readonly FakeClock _clock = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeMfaChallengeRepository _challenges = new();
    private readonly FakeMfaRecoveryCodeRepository _recoveryCodes = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTotpService _totp = new();
    private readonly FakeSecretProtector _protector = new();
    private readonly FakeJwtTokenGenerator _jwt = new();
    private readonly FakeClientInfoProvider _clientInfo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeAuditContext _audit = new();
    private readonly SecurityOptions _options = new();

    private User AddAdmin(bool enrolled = false)
    {
        var user = User.Reconstitute(
            Guid.NewGuid(), AdminEmail, _hasher.Hash(Password), "Admin User", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        if (enrolled)
        {
            user.StartMfaEnrollment(_protector.Protect("SECRET_BASE32", user.Id));
            user.CompleteMfaEnrollment(_clock, step: 100);
        }
        _users.Add(user);
        return user;
    }

    private LoginUseCase CreateLogin() =>
        new(_users, _refreshTokens, _hasher, _jwt, _clientInfo, _unitOfWork, _clock, _audit,
            _options.RefreshTokenDays, _options.MaxFailedAttempts, _options.LockWindowMinutes, _challenges, _options.Mfa);

    private BeginMfaEnrollmentUseCase CreateBeginEnroll() =>
        new(_challenges, _users, _totp, _protector, _unitOfWork, _clock);

    private ConfirmMfaEnrollmentUseCase CreateConfirmEnroll() =>
        new(_challenges, _recoveryCodes, _users, _refreshTokens, _totp, _protector, _jwt, _clientInfo, _unitOfWork, _clock, _audit, _options);

    private VerifyMfaUseCase CreateVerify() =>
        new(_challenges, _recoveryCodes, _users, _refreshTokens, _totp, _protector, _jwt, _clientInfo, _unitOfWork, _clock, _audit, _options);

    private ResetUserMfaUseCase CreateReset() =>
        new(_users, _refreshTokens, _recoveryCodes, _currentUser, _unitOfWork, _clock, _audit);

    [Fact]
    public async Task Login_AnalystWithoutMfa_ReturnsAuthenticatedWithTokens()
    {
        var analyst = User.Reconstitute(
            Guid.NewGuid(), "analyst@loan.test", _hasher.Hash(Password), "Ana", SecurityRole.ROLE_ANALYST, UserStatus.ACTIVE);
        _users.Add(analyst);

        var result = await CreateLogin().ExecuteAsync(new LoginRequest("analyst@loan.test", Password));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("AUTHENTICATED");
        result.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.MfaToken.ShouldBeNull();
    }

    [Fact]
    public async Task Login_EnrolledAdmin_ReturnsMfaRequiredWithoutTokensAndWithoutResettingFailedCount()
    {
        var admin = AddAdmin(enrolled: true);
        admin.RegisterFailedLogin(_clock, 5, TimeSpan.FromMinutes(15));

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("MFA_REQUIRED");
        result.Value.MfaToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.AccessToken.ShouldBeNull();
        result.Value.RefreshToken.ShouldBeNull();
        admin.FailedLoginCount.ShouldBe((short)1);
        _refreshTokens.Added.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Login_UnenrolledAdmin_ReturnsMfaEnrollmentRequired()
    {
        AddAdmin(enrolled: false);

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("MFA_ENROLLMENT_REQUIRED");
        result.Value.MfaToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FullEnrollmentFlow_EnablesMfaGeneratesRecoveryCodesAndIssuesTokens()
    {
        var admin = AddAdmin(enrolled: false);
        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        var enroll = await CreateBeginEnroll().ExecuteAsync(new MfaEnrollRequest(login.Value.MfaToken!));
        enroll.IsSuccess.ShouldBeTrue();
        enroll.Value.Secret.ShouldNotBeNullOrWhiteSpace();
        enroll.Value.OtpauthUri.ShouldContain("otpauth://totp/");

        var confirm = await CreateConfirmEnroll().ExecuteAsync(new MfaEnrollConfirmRequest(login.Value.MfaToken!, ValidCode));
        confirm.IsSuccess.ShouldBeTrue();
        confirm.Value.Tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        confirm.Value.RecoveryCodes.Count.ShouldBe(10);
        admin.MfaEnabled.ShouldBeTrue();
        admin.LastLoginAt.ShouldBe(_clock.UtcNow);
        _audit.Action.ShouldBe("MFA_ENROLLED");
    }

    [Fact]
    public async Task VerifyMfa_WithValidTotp_IssuesTokensAndResetsLockoutCounter()
    {
        var admin = AddAdmin(enrolled: true);
        admin.RegisterFailedLogin(_clock, 5, TimeSpan.FromMinutes(15));
        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        _totp.NextStep = 101;
        var verify = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: ValidCode));

        verify.IsSuccess.ShouldBeTrue();
        verify.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        admin.FailedLoginCount.ShouldBe((short)0);
        admin.LastLoginAt.ShouldBe(_clock.UtcNow);
        _audit.Action.ShouldBe("LOGIN");
    }

    [Fact]
    public async Task VerifyMfa_WithInvalidCode_IncrementsFailedAttemptsAndLocksOnFifth()
    {
        var admin = AddAdmin(enrolled: true);
        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));
        var verifyUseCase = CreateVerify();

        for (var i = 1; i <= 4; i++)
        {
            var fail = await verifyUseCase.ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: "000000"));
            fail.IsFailure.ShouldBeTrue();
            fail.Error.Type.ShouldBe(ErrorType.Unauthorized);
            _audit.Action.ShouldBe("MFA_FAILED");
        }

        var fifth = await verifyUseCase.ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: "000000"));
        fifth.IsFailure.ShouldBeTrue();
        fifth.Error.Type.ShouldBe(ErrorType.Locked);
        admin.IsLockedOut(_clock).ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyMfa_ReplaySameStep_ReturnsUnauthorized()
    {
        AddAdmin(enrolled: true);
        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        _totp.NextStep = 100; // same as enrolled step
        var replay = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: ValidCode));

        replay.IsFailure.ShouldBeTrue();
        replay.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task VerifyMfa_WithEnrollChallenge_ReturnsUnauthorized()
    {
        AddAdmin(enrolled: false);
        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));

        var verify = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: ValidCode));

        verify.IsFailure.ShouldBeTrue();
        verify.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task VerifyMfa_WithRecoveryCode_WorksOnceAndCannotBeReused()
    {
        var admin = AddAdmin(enrolled: true);
        var recovery = MfaRecoveryCode.Create(admin.Id, RecoveryCodeGenerator.Hash("ABCD-EFGH"), _clock);
        _recoveryCodes.AddRange([recovery]);

        var login1 = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));
        var first = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login1.Value.MfaToken!, RecoveryCode: "abcd-efgh"));
        first.IsSuccess.ShouldBeTrue();

        var login2 = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));
        var second = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login2.Value.MfaToken!, RecoveryCode: "ABCD-EFGH"));
        second.IsFailure.ShouldBeTrue();
        second.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task ResetUserMfa_SelfResetReturnsConflict_OtherAdminSucceeds()
    {
        var target = AddAdmin(enrolled: true);
        var otherAdmin = User.Reconstitute(
            Guid.NewGuid(), "other@loan.test", _hasher.Hash(Password), "Other", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(otherAdmin);

        _currentUser.UserId = target.Id;
        var selfResult = await CreateReset().ExecuteAsync(new ResetUserMfaRequest(target.Id));
        selfResult.IsFailure.ShouldBeTrue();
        selfResult.Error.Type.ShouldBe(ErrorType.Conflict);

        _currentUser.UserId = otherAdmin.Id;
        var okResult = await CreateReset().ExecuteAsync(new ResetUserMfaRequest(target.Id));
        okResult.IsSuccess.ShouldBeTrue();
        target.MfaEnabled.ShouldBeFalse();
        target.MfaSecretEncrypted.ShouldBeNull();
        _audit.Action.ShouldBe("MFA_RESET");
    }

    [Fact]
    public async Task VerifyMfa_WithOutdatedProtectionVersion_LazilyReencryptsSecret()
    {
        var admin = AddAdmin(enrolled: true);
        admin.MfaSecretEncrypted!.ShouldStartWith("v2:k1:");

        _protector.ActiveVersion = "v2:k2";

        var login = await CreateLogin().ExecuteAsync(new LoginRequest(AdminEmail, Password));
        login.IsSuccess.ShouldBeTrue();

        var verify = await CreateVerify().ExecuteAsync(new MfaVerifyRequest(login.Value.MfaToken!, Code: ValidCode));
        verify.IsSuccess.ShouldBeTrue();
        admin.MfaSecretEncrypted.ShouldStartWith($"v2:k2:{admin.Id}::");
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _items = [];
        public void Add(User u) => _items.Add(u);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(u => u.Email == email));
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(u => u.Id == id));
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Added { get; } = [];
        public void Add(RefreshToken token) => Added.Add(token);
        public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(Added.FirstOrDefault(t => t.TokenHash == tokenHash && t.RevokedAt == null));
        public Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
        {
            foreach (var t in Added.Where(t => t.UserId == userId && t.RevokedAt == null)) t.Revoke(clock);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMfaChallengeRepository : IMfaChallengeRepository
    {
        private readonly List<MfaChallenge> _items = [];
        public void Add(MfaChallenge challenge) => _items.Add(challenge);
        public Task<MfaChallenge?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.TokenHash == tokenHash));
    }

    private sealed class FakeMfaRecoveryCodeRepository : IMfaRecoveryCodeRepository
    {
        private readonly List<MfaRecoveryCode> _items = [];
        public void AddRange(IEnumerable<MfaRecoveryCode> codes) => _items.AddRange(codes);
        public Task<IReadOnlyList<MfaRecoveryCode>> FindUnusedAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MfaRecoveryCode>>(_items.Where(c => c.UserId == userId && c.IsUnused).ToList());
        public Task MarkAllUsedForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
        {
            foreach (var c in _items.Where(c => c.UserId == userId && c.IsUnused)) c.Use(clock);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed::{password}";
        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class FakeTotpService : ITotpService
    {
        public long NextStep { get; set; } = 200;
        public string GenerateSecret() => "JBSWY3DPEHPK3PXP";
        public string BuildProvisioningUri(string secret, string accountName) =>
            $"otpauth://totp/LoanSystem:{accountName}?secret={secret}&issuer=LoanSystem";
        public bool TryVerify(string secret, string code, DateTimeOffset now, out long matchedStep)
        {
            matchedStep = NextStep;
            return code == ValidCode;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string ActiveVersion { get; set; } = "v2:k1";
        public string Protect(string plaintext, Guid userId) => $"{ActiveVersion}:{userId}::{plaintext}";
        public string Unprotect(string protectedValue, Guid userId)
        {
            var idx = protectedValue.LastIndexOf("::", StringComparison.Ordinal);
            return idx >= 0 ? protectedValue[(idx + 2)..] : protectedValue;
        }
        public bool NeedsReprotect(string protectedValue) =>
            !protectedValue.StartsWith($"{ActiveVersion}:", StringComparison.Ordinal);
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user) =>
            ($"jwt::{user.Id}", new DateTimeOffset(2026, 11, 13, 12, 30, 0, TimeSpan.Zero));
    }

    private sealed class FakeClientInfoProvider : IClientInfoProvider
    {
        public IPAddress? IpAddress => IPAddress.Loopback;
        public string? UserAgent => "UnitTest";
    }

    private sealed class FakeUnitOfWork : ISecurityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public string? Email => "admin@loan.test";
        public IReadOnlyCollection<string> Roles => ["ROLE_ADMIN"];
        public Guid? CustomerId => null;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class FakeAuditContext : IAuditContext
    {
        private readonly Dictionary<string, string> _entityActions = new(StringComparer.OrdinalIgnoreCase);
        public string? Action { get; private set; }
        public Guid? ActorId { get; private set; }
        public Guid? ApprovalRequestId { get; private set; }
        public void SetAction(string action) => Action = action;
        public void SetAction(string action, string entityName) => _entityActions[entityName] = action;
        public string? GetActionForEntity(string entityName) => _entityActions.TryGetValue(entityName, out var a) ? a : null;
        public void SetActor(Guid userId) => ActorId = userId;
        public void SetApproval(Guid approvalRequestId) => ApprovalRequestId = approvalRequestId;
        public void Clear() { Action = null; ActorId = null; ApprovalRequestId = null; _entityActions.Clear(); }
    }
}
