using System.Text.Json;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Application.Approvals;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class ApprovalUseCasesTests
{
    private const string ValidTotp = "654321";

    private readonly FakeClock _clock = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeApprovalRepository _approvals = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeStatusTracker _statusTracker = new();
    private readonly FakeAuditContext _audit = new();
    private readonly FakeTotpService _totp = new();
    private readonly FakeSecretProtector _protector = new();
    private readonly ApprovalsOptions _options = new();

    private User AddUser(string email, SecurityRole role, bool mfaEnrolled = true)
    {
        var user = User.Reconstitute(Guid.NewGuid(), email, "hash", email, role, UserStatus.ACTIVE);
        if (mfaEnrolled)
        {
            user.StartMfaEnrollment(_protector.Protect("SECRET", user.Id));
            user.CompleteMfaEnrollment(_clock, step: 100);
        }
        _users.Add(user);
        return user;
    }

    private RequestUserRoleChangeUseCase CreateRequestRoleChange() =>
        new(_currentUser, _users, _approvals, _clock, _audit, _options);

    private UserRoleChangeApprovalHandler CreateRoleChangeHandler() =>
        new(_approvals, _users, _refreshTokens, _unitOfWork, _statusTracker, _clock, _audit);

    private ApproveRequestUseCase CreateApprove(params IApprovalHandler[] handlers) =>
        new(_currentUser, _users, _approvals, _totp, _protector, _unitOfWork, _clock, _audit, _options, handlers);

    private RejectRequestUseCase CreateReject() =>
        new(_currentUser, _users, _approvals, _clock, _audit);

    private CancelRequestUseCase CreateCancel() =>
        new(_currentUser, _approvals, _clock, _audit);

    private SuspendUserUseCase CreateSuspend() =>
        new(_users, _refreshTokens, _currentUser, _unitOfWork, _clock, _statusTracker, _audit, _approvals);

    private ExpireApprovalRequestsUseCase CreateExpire() =>
        new(_approvals, _clock, _audit);

    [Fact]
    public async Task RequestUserRoleChange_CreatesPendingWithoutModifyingUser_AndRejectsDuplicate()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("analyst@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);
        _currentUser.Authenticate(adminA);

        var first = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Promoción por cobertura operativa"));

        first.IsSuccess.ShouldBeTrue();
        first.Value.Status.ShouldBe("PENDING");
        target.Role.ShouldBe(SecurityRole.ROLE_ANALYST); // unchanged until approved

        var duplicate = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Segundo intento duplicado"));

        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Error.Code.ShouldBe("Approvals.DuplicatePending");
    }

    [Fact]
    public async Task ApproveRequest_WithoutPermission_ReturnsForbiddenBeforeWriting()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var analyst = AddUser("analyst@loan.test", SecurityRole.ROLE_ANALYST);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_COLLECTOR, mfaEnrolled: false);

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Cambio de rol autorizado"));

        _currentUser.Authenticate(analyst);
        var approve = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "Intentando aprobar", ValidTotp));

        approve.IsFailure.ShouldBeTrue();
        approve.Error.Type.ShouldBe(ErrorType.Forbidden);
        approve.Error.Code.ShouldBe("Approvals.NotAllowed");
        target.Role.ShouldBe(SecurityRole.ROLE_COLLECTOR);
    }

    [Fact]
    public async Task ApproveRequest_SelfOrInvalidMfa_Returns403WithoutExecuting()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Asignar rol de operador"));

        // Self-approval blocked
        var selfApprove = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "Auto", ValidTotp));
        selfApprove.Error.Code.ShouldBe("Approvals.SelfApprovalForbidden");

        // Wrong TOTP returns 403 (never 401)
        _currentUser.Authenticate(adminB);
        var badTotp = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "Aprobado", "000000"));
        badTotp.IsFailure.ShouldBeTrue();
        badTotp.Error.Type.ShouldBe(ErrorType.Forbidden);
        badTotp.Error.Code.ShouldBe("Approvals.InvalidMfaCode");

        // Replayed TOTP step returns 403
        _totp.NextStep = 100; // already used at enrollment
        var replay = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "Aprobado", ValidTotp));
        replay.Error.Code.ShouldBe("Approvals.InvalidMfaCode");
    }

    [Fact]
    public async Task ApproveRequest_ValidSecondAdminAndTotp_ExecutesRoleChangeRevokesTokensAndEvictsCache()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);
        _refreshTokens.Add(RefreshToken.Issue(target.Id, "tok-hash", _clock, TimeSpan.FromDays(7), null, null));

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Cambio de área operativa"));

        _currentUser.Authenticate(adminB);
        _totp.NextStep = 101;
        var approve = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "Revisado y aprobado", ValidTotp));

        approve.IsSuccess.ShouldBeTrue();
        approve.Value.Status.ShouldBe("APPROVED");
        target.Role.ShouldBe(SecurityRole.ROLE_OPERATOR);
        _refreshTokens.AllRevokedFor(target.Id).ShouldBeTrue();
        _statusTracker.EvictedUsers.ShouldContain(target.Id);
        _audit.ApprovalRequestId.ShouldBe(req.Value.Id);
        _audit.GetActionForEntity("approval_requests").ShouldBe("APPROVE");
        _audit.GetActionForEntity("users").ShouldBe("ROLE_CHANGE");
    }

    [Fact]
    public async Task ApproveRequest_WhenTargetChangedSinceSubmission_ReturnsConflictAndRollsBack()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Cambio de rol inicial"));

        // Target role mutated externally before approval
        target.ChangeRole(SecurityRole.ROLE_COLLECTOR);

        _currentUser.Authenticate(adminB);
        _totp.NextStep = 102;
        var approve = await CreateApprove(CreateRoleChangeHandler())
            .ExecuteAsync(new ApproveApprovalCommand(req.Value.Id, "OK", ValidTotp));

        approve.IsFailure.ShouldBeTrue();
        approve.Error.Type.ShouldBe(ErrorType.Conflict);
        approve.Error.Code.ShouldBe("Approvals.TargetChanged");
        _unitOfWork.LastTransactionRolledBack.ShouldBeTrue();
    }

    [Fact]
    public async Task SuspendRequester_CancelsTheirPendingApprovalRequests()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Solicitud que quedará huérfana"));

        _currentUser.Authenticate(adminB);
        var suspend = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(adminA.Id));
        suspend.IsSuccess.ShouldBeTrue();

        var stored = await _approvals.FindByIdAsync(req.Value.Id);
        stored!.Status.ShouldBe(ApprovalStatus.CANCELLED);
    }

    [Fact]
    public async Task ExpireApprovalRequests_ExpiresOnlyOverduePendingRequests()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);
        _currentUser.Authenticate(adminA);

        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Solicitud con vencimiento"));

        var before = await CreateExpire().ExecuteAsync();
        before.Value.ShouldBe(0);

        _clock.Advance(TimeSpan.FromHours(73));
        var after = await CreateExpire().ExecuteAsync();
        after.Value.ShouldBe(1);

        var stored = await _approvals.FindByIdAsync(req.Value.Id);
        stored!.Status.ShouldBe(ApprovalStatus.EXPIRED);
    }

    [Fact]
    public async Task RejectAndCancel_FollowAuthorizationAndStateRules()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);

        _currentUser.Authenticate(adminA);
        var req1 = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Solicitud 1 para cancelar"));

        _currentUser.Authenticate(adminB);
        var cancelDenied = await CreateCancel().ExecuteAsync(req1.Value.Id);
        cancelDenied.Error.Type.ShouldBe(ErrorType.Forbidden);

        _currentUser.Authenticate(adminA);
        var cancelled = await CreateCancel().ExecuteAsync(req1.Value.Id);
        cancelled.IsSuccess.ShouldBeTrue();
        cancelled.Value.Status.ShouldBe("CANCELLED");

        var req2 = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Solicitud 2 para rechazar"));

        _currentUser.Authenticate(adminB);
        var rejected = await CreateReject().ExecuteAsync(new RejectApprovalCommand(req2.Value.Id, "No corresponde"));
        rejected.IsSuccess.ShouldBeTrue();
        rejected.Value.Status.ShouldBe("REJECTED");
    }

    [Fact]
    public async Task Approve_WithOutdatedProtectionVersion_LazilyReencryptsApproverSecret()
    {
        var adminA = AddUser("admina@loan.test", SecurityRole.ROLE_ADMIN);
        var adminB = AddUser("adminb@loan.test", SecurityRole.ROLE_ADMIN);
        var target = AddUser("target@loan.test", SecurityRole.ROLE_ANALYST, mfaEnrolled: false);

        adminB.MfaSecretEncrypted!.ShouldStartWith("v2:k1:");

        _currentUser.Authenticate(adminA);
        var req = await CreateRequestRoleChange().ExecuteAsync(
            new RequestUserRoleChangeCommand(target.Id, "ROLE_OPERATOR", "Promoción operativa"));
        req.IsSuccess.ShouldBeTrue();

        // Rotate active MFA key before Admin B approves via MFA step-up
        _protector.ActiveVersion = "v2:k2";

        _currentUser.Authenticate(adminB);
        var approve = await CreateApprove().ExecuteAsync(
            new ApproveApprovalCommand(req.Value.Id, "Aprobado", ValidCode));

        approve.IsSuccess.ShouldBeTrue();
        adminB.MfaSecretEncrypted.ShouldStartWith($"v2:k2:{adminB.Id}::");
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
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
        private readonly List<RefreshToken> _tokens = [];
        public void Add(RefreshToken token) => _tokens.Add(token);
        public bool AllRevokedFor(Guid userId) => _tokens.Where(t => t.UserId == userId).All(t => t.RevokedAt.HasValue);
        public Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash && t.RevokedAt == null));
        public Task RevokeAllForUserAsync(Guid userId, IClock clock, CancellationToken ct = default)
        {
            foreach (var t in _tokens.Where(t => t.UserId == userId && t.RevokedAt == null)) t.Revoke(clock);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApprovalRepository : ISecurityApprovalRequests, IApprovalRequestReader
    {
        private readonly List<ApprovalRequest> _items = [];
        public void Add(ApprovalRequest request) => _items.Add(request);
        public Task<ApprovalRequest?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(a => a.Id == id));
        public Task<bool> ExistsPendingAsync(string operationType, Guid targetId, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(a => a.OperationType == operationType && a.TargetId == targetId && a.Status == ApprovalStatus.PENDING));
        public Task<IReadOnlyList<ApprovalRequest>> FindPendingByRequesterAsync(Guid requesterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>(_items.Where(a => a.RequestedBy == requesterId && a.Status == ApprovalStatus.PENDING).ToList());
        public Task<IReadOnlyList<ApprovalRequest>> FindExpiredPendingAsync(IClock clock, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalRequest>>(_items.Where(a => a.Status == ApprovalStatus.PENDING && a.ExpiresAt <= clock.UtcNow).ToList());
        public Task<Result> SaveApprovalChangesAsync(CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<ApprovalRequestHeader?> GetHeaderAsync(Guid id, CancellationToken ct = default)
        {
            var r = _items.FirstOrDefault(a => a.Id == id);
            return Task.FromResult(r is null ? null : new ApprovalRequestHeader(
                r.Id, r.OperationType, r.Module, r.TargetEntity, r.TargetId, r.Status, r.RequestedBy, r.ExpiresAt, r.PayloadJson));
        }

        public Task<ApprovalRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ApprovalRequestDto?>(null);

        public Task<PagedResult<ApprovalRequestSummary>> QueryAsync(ApprovalQuery query, Guid currentUserId, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<ApprovalRequestSummary>([], 0, query.Page, query.PageSize));
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

    private sealed class FakeTransactionScope(FakeUnitOfWork owner) : ITransactionScope
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct = default) { owner.LastTransactionRolledBack = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeUnitOfWork : ISecurityUnitOfWork
    {
        public bool LastTransactionRolledBack { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
        {
            LastTransactionRolledBack = false;
            return Task.FromResult<ITransactionScope>(new FakeTransactionScope(this));
        }
    }

    private sealed class FakeStatusTracker : IUserStatusTracker
    {
        public List<Guid> EvictedUsers { get; } = [];
        public void InvalidateUser(Guid userId) { }
        public void ActivateUser(Guid userId) { }
        public void EvictUser(Guid userId) => EvictedUsers.Add(userId);
    }

    private sealed class FakeTotpService : ITotpService
    {
        public long NextStep { get; set; } = 200;
        public string GenerateSecret() => "JBSWY3DPEHPK3PXP";
        public string BuildProvisioningUri(string secret, string accountName) => $"otpauth://totp/{accountName}";
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
    }
}
