using System.Net;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Shouldly;
using Xunit;
using IClientInfoProvider = LoanSystem.Modules.Security.Core.Ports.Outbound.IClientInfoProvider;

namespace LoanSystem.Application.UnitTests.Customers;

/// <summary>
/// Verifies R7: Security use cases resolve and propagate customer_id into JWT generation for ROLE_CLIENT users.
/// </summary>
public class SecurityClientPortalJwtTests
{
    private const string ClientEmail = "portal.client@loan.test";
    private const string Password = "Client-Password-123";
    private const string ValidTotp = "654321";

    private readonly FakeClock _clock = new();
    private readonly FakeUsers _users = new();
    private readonly FakeRefreshTokens _refreshTokens = new();
    private readonly FakeChallenges _challenges = new();
    private readonly FakeRecoveryCodes _recovery = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeTotp _totp = new();
    private readonly FakeProtector _protector = new();
    private readonly CapturingJwtGenerator _jwt = new();
    private readonly FakeClientInfo _clientInfo = new();
    private readonly FakeUow _uow = new();
    private readonly FakeAudit _audit = new();
    private readonly StubCustomerPortalLookup _portalLookup = new();

    private User AddClientUser(bool withMfaEnrolled = false)
    {
        var user = User.Reconstitute(
            Guid.NewGuid(), ClientEmail, _hasher.Hash(Password), "Cliente Portal", SecurityRole.ROLE_CLIENT, UserStatus.ACTIVE);
        if (withMfaEnrolled)
        {
            user.StartMfaEnrollment($"v2:k1:{user.Id}::SECRET");
            user.CompleteMfaEnrollment(_clock, step: 10);
        }
        _users.Add(user);
        return user;
    }

    [Fact]
    public async Task Login_ForRoleClient_ResolvesCustomerIdAndPassesItToJwtGenerator()
    {
        var user = AddClientUser();
        var customerId = Guid.NewGuid();
        _portalLookup.Map[user.Id] = customerId;

        var loginUc = new LoginUseCase(
            _users, _refreshTokens, _hasher, _jwt, _clientInfo, _uow, _clock, _audit,
            refreshTokenDays: 14, portalLookup: _portalLookup);

        var result = await loginUc.ExecuteAsync(new LoginRequest(ClientEmail, Password));

        result.IsSuccess.ShouldBeTrue();
        _jwt.LastCustomerId.ShouldBe(customerId);
    }

    [Fact]
    public async Task Login_ForRoleClientWithoutLinkedCustomer_PassesNullCustomerIdSafely()
    {
        AddClientUser();

        var loginUc = new LoginUseCase(
            _users, _refreshTokens, _hasher, _jwt, _clientInfo, _uow, _clock, _audit,
            refreshTokenDays: 14, portalLookup: _portalLookup);

        var result = await loginUc.ExecuteAsync(new LoginRequest(ClientEmail, Password));

        result.IsSuccess.ShouldBeTrue();
        _jwt.LastCustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshToken_ForRoleClient_ReResolvesCustomerId()
    {
        var user = AddClientUser();
        var customerId = Guid.NewGuid();
        _portalLookup.Map[user.Id] = customerId;

        var loginUc = new LoginUseCase(
            _users, _refreshTokens, _hasher, _jwt, _clientInfo, _uow, _clock, _audit,
            refreshTokenDays: 14, portalLookup: _portalLookup);
        var loginRes = await loginUc.ExecuteAsync(new LoginRequest(ClientEmail, Password));

        _jwt.Reset();
        var refreshUc = new RefreshTokenUseCase(
            _refreshTokens, _users, _jwt, _clientInfo, _uow, _clock, _audit,
            refreshTokenDays: 14, portalLookup: _portalLookup);

        var refreshRes = await refreshUc.ExecuteAsync(new RefreshRequest(loginRes.Value.RefreshToken!));

        refreshRes.IsSuccess.ShouldBeTrue();
        _jwt.LastCustomerId.ShouldBe(customerId);
    }

    [Fact]
    public async Task MfaVerifyAndConfirmEnrollment_ForRoleClient_IncludeCustomerIdInJwt()
    {
        var user = AddClientUser(withMfaEnrolled: true);
        var customerId = Guid.NewGuid();
        _portalLookup.Map[user.Id] = customerId;
        var mfaOptions = new MfaOptions { RequiredRoles = new HashSet<SecurityRole> { SecurityRole.ROLE_CLIENT } };

        var loginUc = new LoginUseCase(
            _users, _refreshTokens, _hasher, _jwt, _clientInfo, _uow, _clock, _audit,
            14, 5, 15, _challenges, mfaOptions, _portalLookup);
        var verifyUc = new VerifyMfaUseCase(
            _challenges, _recovery, _users, _refreshTokens, _totp, _protector, _jwt, _clientInfo, _uow, _clock, _audit,
            new SecurityOptions { Mfa = mfaOptions }, _portalLookup);

        var challengeRes = await loginUc.ExecuteAsync(new LoginRequest(ClientEmail, Password));
        challengeRes.Value.Status.ShouldBe("MFA_REQUIRED");

        var verifyRes = await verifyUc.ExecuteAsync(new MfaVerifyRequest(challengeRes.Value.MfaToken!, ValidTotp));
        verifyRes.IsSuccess.ShouldBeTrue();
        _jwt.LastCustomerId.ShouldBe(customerId);

        // Also test ConfirmMfaEnrollmentUseCase propagates customer_id
        var enrollUser = User.Reconstitute(
            Guid.NewGuid(), "enroll.client@loan.test", _hasher.Hash(Password), "Enroll", SecurityRole.ROLE_CLIENT, UserStatus.ACTIVE);
        _users.Add(enrollUser);
        var enrollCid = Guid.NewGuid();
        _portalLookup.Map[enrollUser.Id] = enrollCid;
        _jwt.Reset();

        var enrollChallenge = await loginUc.ExecuteAsync(new LoginRequest("enroll.client@loan.test", Password));
        enrollChallenge.Value.Status.ShouldBe("MFA_ENROLLMENT_REQUIRED");

        var beginEnroll = new BeginMfaEnrollmentUseCase(_challenges, _users, _totp, _protector, _uow, _clock);
        (await beginEnroll.ExecuteAsync(new MfaEnrollRequest(enrollChallenge.Value.MfaToken!))).IsSuccess.ShouldBeTrue();

        var confirmEnroll = new ConfirmMfaEnrollmentUseCase(
            _challenges, _recovery, _users, _refreshTokens, _totp, _protector, _jwt, _clientInfo, _uow, _clock, _audit,
            new SecurityOptions { Mfa = mfaOptions }, _portalLookup);
        var confirmRes = await confirmEnroll.ExecuteAsync(new MfaEnrollConfirmRequest(enrollChallenge.Value.MfaToken!, ValidTotp));
        confirmRes.IsSuccess.ShouldBeTrue();
        _jwt.LastCustomerId.ShouldBe(enrollCid);
    }

    private sealed class StubCustomerPortalLookup : ICustomerPortalLookup
    {
        public Dictionary<Guid, Guid> Map { get; } = [];
        public Task<Guid?> FindCustomerIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default) =>
            Task.FromResult<Guid?>(Map.TryGetValue(portalUserId, out var cid) ? cid : null);
    }

    private sealed class CapturingJwtGenerator : IJwtTokenGenerator
    {
        public Guid? LastCustomerId { get; private set; }
        public void Reset() => LastCustomerId = null;

        public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user) =>
            Generate(user, null);

        public (string AccessToken, DateTimeOffset ExpiresAt) Generate(User user, Guid? customerId)
        {
            LastCustomerId = customerId;
            return ($"jwt::{user.Id}::cid={customerId}", DateTimeOffset.UtcNow.AddMinutes(30));
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly List<User> _items = [];
        public void Add(User u) => _items.Add(u);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(u => u.Email == email));
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(u => u.Id == id));
    }

    private sealed class FakeRefreshTokens : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _items = [];
        public void Add(RefreshToken token) => _items.Add(token);
        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(t => t.TokenHash == tokenHash));
        public Task RevokeFamilyAsync(Guid familyId, IClock clock, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChallenges : IMfaChallengeRepository
    {
        private readonly List<MfaChallenge> _items = [];
        public void Add(MfaChallenge c) => _items.Add(c);
        public Task<MfaChallenge?> FindByHashAsync(string hash, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(c => c.TokenHash == hash));
    }

    private sealed class FakeRecoveryCodes : IMfaRecoveryCodeRepository
    {
        public void AddRange(IEnumerable<MfaRecoveryCode> codes) { }
        public Task<IReadOnlyList<MfaRecoveryCode>> FindUnusedAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MfaRecoveryCode>>([]);
        public Task MarkAllUsedForUserAsync(Guid userId, IClock clock, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string p) => $"H::{p}";
        public bool Verify(string p, string h) => h == $"H::{p}";
    }

    private sealed class FakeTotp : ITotpService
    {
        public string GenerateSecret() => "SECRET";
        public string BuildProvisioningUri(string s, string a) => "otpauth://totp/test";
        public bool TryVerify(string s, string code, DateTimeOffset now, out long matchedStep)
        {
            matchedStep = 200;
            return code == ValidTotp;
        }
    }

    private sealed class FakeProtector : ISecretProtector
    {
        public string ActiveVersion => "v2:k1";
        public string Protect(string pt, Guid uid) => $"{ActiveVersion}:{uid}::{pt}";
        public string Unprotect(string pv, Guid uid) => pv.Split("::")[^1];
        public bool NeedsReprotect(string pv) => false;
    }

    private sealed class FakeClientInfo : IClientInfoProvider
    {
        public IPAddress? IpAddress => IPAddress.Loopback;
        public string? UserAgent => "UnitTest";
    }

    private sealed class FakeUow : ISecurityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAudit : IAuditContext
    {
        public string? Action { get; private set; }
        public Guid? ActorId { get; private set; }
        public Guid? ApprovalRequestId { get; private set; }
        public void SetAction(string a) => Action = a;
        public void SetAction(string a, string entity) { }
        public string? GetActionForEntity(string entity) => null;
        public void SetActor(Guid id) => ActorId = id;
        public void SetApproval(Guid id) => ApprovalRequestId = id;
    }
}
