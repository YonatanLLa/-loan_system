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
    private readonly FakeStatusTracker _statusTracker = new();
    private readonly FakeAuditContext _audit = new();

    private User AddActiveUser()
    {
        var user = User.Reconstitute(
            Guid.NewGuid(), Email, _hasher.Hash(Password), "Ana Lista", SecurityRole.ROLE_ANALYST, UserStatus.ACTIVE);
        _users.Add(user);
        return user;
    }

    private LoginUseCase CreateLogin() =>
        new(_users, _refreshTokens, _hasher, _jwt, _clientInfo, _unitOfWork, _clock, _audit, refreshTokenDays: 14);

    private RefreshTokenUseCase CreateRefresh() =>
        new(_refreshTokens, _users, _jwt, _clientInfo, _unitOfWork, _clock, _audit, refreshTokenDays: 14);

    private LogoutUseCase CreateLogout() =>
        new(_refreshTokens, _currentUser, _unitOfWork, _clock);

    private GetCurrentUserUseCase CreateGetMe() =>
        new(_currentUser, _users);

    private SuspendUserUseCase CreateSuspend() =>
        new(_users, _refreshTokens, _currentUser, _unitOfWork, _clock, _statusTracker, _audit);

    private ActivateUserUseCase CreateActivate() =>
        new(_users, _statusTracker, _unitOfWork, _audit);

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
        stored.TokenHash.ShouldBe(RefreshTokenHasher.Hash(result.Value.RefreshToken!));
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
        result.Error.Code.ShouldBe("Security.InvalidRefreshToken");
        _refreshTokens.RevokeFamilyCalls.ShouldBe(0);       // expiry is not a reuse signal
        expired.RevokedAt.ShouldBeNull();
        _unitOfWork.SaveChangesCalls.ShouldBe(0);
    }

    // ----- Refresh token families, reuse detection and atomic rotation (DECISION_02) -----

    [Fact]
    public async Task Login_StartsANewTokenFamily()
    {
        AddActiveUser();

        await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        var issued = _refreshTokens.Added.ShouldHaveSingleItem();
        issued.FamilyId.ShouldBe(issued.Id);
        issued.ReplacedByTokenId.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_Rotation_KeepsFamilyAndLinksOldTokenToItsReplacement()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        var existing = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(existing);

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsSuccess.ShouldBeTrue();
        var rotated = _refreshTokens.Added.ShouldHaveSingleItem();
        rotated.FamilyId.ShouldBe(existing.FamilyId);
        rotated.Id.ShouldNotBe(existing.Id);
        rotated.IsActive(_clock).ShouldBeTrue();
        rotated.TokenHash.ShouldBe(RefreshTokenHasher.Hash(result.Value.RefreshToken));
        existing.RevokedAt.ShouldBe(_clock.UtcNow);
        existing.ReplacedByTokenId.ShouldBe(rotated.Id);
        _refreshTokens.RevokeFamilyCalls.ShouldBe(0);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);           // INSERT + UPDATE in a single save
    }

    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesTheWholeFamilyAndReturnsReused()
    {
        var user = AddActiveUser();
        var (rawStolen, hash) = RefreshTokenHasher.Create();
        var original = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(original);

        // Another session (other device) of the same user: a different family.
        var (_, otherHash) = RefreshTokenHasher.Create();
        var otherSession = RefreshToken.Issue(user.Id, otherHash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(otherSession);

        var useCase = CreateRefresh();
        var first = await useCase.ExecuteAsync(new RefreshRequest(rawStolen));   // legitimate rotation
        first.IsSuccess.ShouldBeTrue();
        var second = await useCase.ExecuteAsync(new RefreshRequest(first.Value.RefreshToken));
        second.IsSuccess.ShouldBeTrue();
        _audit.SetActor(Guid.Empty);                                              // reset to observe the reuse path

        var reuse = await useCase.ExecuteAsync(new RefreshRequest(rawStolen));   // the old token shows up again

        reuse.IsFailure.ShouldBeTrue();
        reuse.Error.Type.ShouldBe(ErrorType.Unauthorized);
        reuse.Error.Code.ShouldBe("Security.RefreshTokenReused");
        _refreshTokens.RevokeFamilyCalls.ShouldBe(1);
        _refreshTokens.Added.ShouldAllBe(t => t.FamilyId == original.FamilyId);
        _refreshTokens.Added.ShouldAllBe(t => t.RevokedAt != null);              // the current head of the chain is revoked too
        otherSession.IsActive(_clock).ShouldBeTrue();                             // other families are untouched
        _audit.ActorId.ShouldBe(user.Id);
        _unitOfWork.SaveChangesCalls.ShouldBe(3);

        // After the family is revoked, the latest token is no longer usable either.
        var afterRevocation = await useCase.ExecuteAsync(new RefreshRequest(second.Value.RefreshToken));
        afterRevocation.IsFailure.ShouldBeTrue();
        afterRevocation.Error.Code.ShouldBe("Security.InvalidRefreshToken");
    }

    [Fact]
    public async Task Refresh_WithLoggedOutToken_ReturnsInvalidWithoutFamilyRevocation()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        _refreshTokens.Seed(RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null));
        await CreateLogout().ExecuteAsync(new LogoutRequest(raw));

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Security.InvalidRefreshToken");   // revoked but never rotated: not a reuse
        _refreshTokens.RevokeFamilyCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_WhenRotationLosesAConcurrencyRace_ReturnsInvalidWithoutRevokingTheFamily()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        var existing = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(existing);
        _unitOfWork.FailNextSaveWithConcurrencyConflict = true;   // another request rotated it first

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Security.InvalidRefreshToken");
        _refreshTokens.RevokeFamilyCalls.ShouldBe(0);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Logout_WhenTokenWasRevokedConcurrently_StillSucceeds()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        _refreshTokens.Seed(RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null));
        _unitOfWork.FailNextSaveWithConcurrencyConflict = true;

        var result = await CreateLogout().ExecuteAsync(new LogoutRequest(raw));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Logout_WithAlreadyRevokedToken_IsANoOp()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        var token = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        token.Revoke(_clock);
        _refreshTokens.Seed(token);

        var result = await CreateLogout().ExecuteAsync(new LogoutRequest(raw));

        result.IsSuccess.ShouldBeTrue();
        _unitOfWork.SaveChangesCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Suspend_RevokesTokensAndSavesInsideOneTransaction()
    {
        var target = AddActiveUser();

        var result = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(target.Id));

        result.IsSuccess.ShouldBeTrue();
        _unitOfWork.CommittedTransactions.ShouldBe(1);
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

    [Fact]
    public async Task Suspend_SetsStatusToInactiveAndRevokesAllRefreshTokens()
    {
        var targetUser = AddActiveUser();
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        var (_, hash1) = RefreshTokenHasher.Create();
        var (_, hash2) = RefreshTokenHasher.Create();
        var token1 = RefreshToken.Issue(targetUser.Id, hash1, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        var token2 = RefreshToken.Issue(targetUser.Id, hash2, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(token1);
        _refreshTokens.Seed(token2);

        var result = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(targetUser.Id));

        result.IsSuccess.ShouldBeTrue();
        targetUser.Status.ShouldBe(UserStatus.INACTIVE);
        targetUser.IsActive.ShouldBeFalse();
        token1.IsActive(_clock).ShouldBeFalse();
        token2.IsActive(_clock).ShouldBeFalse();
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Suspend_WithUnknownUser_ReturnsNotFound()
    {
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        var result = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(Guid.NewGuid()));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Security.UserNotFound");
    }

    [Fact]
    public async Task Suspend_Self_ReturnsConflict()
    {
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        var result = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(admin.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Security.CannotSuspendSelf");
    }

    [Fact]
    public async Task Login_WithSuspendedUser_ReturnsForbidden()
    {
        var user = AddActiveUser();
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        await CreateSuspend().ExecuteAsync(new SuspendUserRequest(user.Id));

        var result = await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Security.UserNotActive");
    }

    [Fact]
    public async Task Refresh_WithSuspendedUserToken_ReturnsUnauthorized()
    {
        var user = AddActiveUser();
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        var (raw, hash) = RefreshTokenHasher.Create();
        var token = RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null);
        _refreshTokens.Seed(token);

        await CreateSuspend().ExecuteAsync(new SuspendUserRequest(user.Id));

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Security.InvalidRefreshToken");
    }

    [Fact]
    public async Task GetCurrentUser_WithSuspendedUser_ReturnsForbidden()
    {
        var user = AddActiveUser();
        _currentUser.Authenticate(user);

        user.Suspend();

        var result = await CreateGetMe().ExecuteAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Security.UserNotActive");
    }

    [Fact]
    public async Task SuspendUser_CallsStatusTrackerInvalidateUser()
    {
        var user = AddActiveUser();
        var admin = User.Reconstitute(
            Guid.NewGuid(), "admin@loan.test", _hasher.Hash(Password), "Admin", SecurityRole.ROLE_ADMIN, UserStatus.ACTIVE);
        _users.Add(admin);
        _currentUser.Authenticate(admin);

        var result = await CreateSuspend().ExecuteAsync(new SuspendUserRequest(user.Id));

        result.IsSuccess.ShouldBeTrue();
        _statusTracker.InvalidatedUsers.ShouldContain(user.Id);
    }

    [Fact]
    public async Task ActivateUser_WithValidUser_RestoresActiveStatusAndCallsTracker()
    {
        var user = AddActiveUser();
        user.Suspend();
        user.Status.ShouldBe(UserStatus.INACTIVE);

        var result = await CreateActivate().ExecuteAsync(new ActivateUserRequest(user.Id));

        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.ACTIVE);
        user.IsActive.ShouldBeTrue();
        _statusTracker.ActivatedUsers.ShouldContain(user.Id);
        _unitOfWork.SaveChangesCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ActivateUser_WithEmptyGuid_ReturnsValidation()
    {
        var result = await CreateActivate().ExecuteAsync(new ActivateUserRequest(Guid.Empty));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Security.InvalidUserId");
    }

    [Fact]
    public async Task ActivateUser_WithNonExistentUser_ReturnsNotFound()
    {
        var result = await CreateActivate().ExecuteAsync(new ActivateUserRequest(Guid.NewGuid()));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Security.UserNotFound");
    }

    // ----- Fakes -------------------------------------------------------------

    [Fact]
    public async Task Login_Success_SetsLoginActionAndActor()
    {
        var user = AddActiveUser();

        await CreateLogin().ExecuteAsync(new LoginRequest(Email, Password));

        _audit.Action.ShouldBe("LOGIN");
        _audit.ActorId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Login_WrongPassword_SetsLoginFailedWithoutActor()
    {
        AddActiveUser();

        await CreateLogin().ExecuteAsync(new LoginRequest(Email, "wrong-password"));

        _audit.Action.ShouldBe("LOGIN_FAILED");
        _audit.ActorId.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_SetsActorToTokenOwner()
    {
        var user = AddActiveUser();
        var (raw, hash) = RefreshTokenHasher.Create();
        _refreshTokens.Seed(RefreshToken.Issue(user.Id, hash, _clock, TimeSpan.FromDays(14), userIp: null, userAgent: null));

        var result = await CreateRefresh().ExecuteAsync(new RefreshRequest(raw));

        result.IsSuccess.ShouldBeTrue();
        _audit.ActorId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Suspend_SetsSuspendAuditAction()
    {
        var target = AddActiveUser();

        await CreateSuspend().ExecuteAsync(new SuspendUserRequest(target.Id));

        _audit.Action.ShouldBe("SUSPEND");
    }

    [Fact]
    public async Task Activate_SetsActivateAuditAction()
    {
        var target = AddActiveUser();
        target.Suspend();

        await CreateActivate().ExecuteAsync(new ActivateUserRequest(target.Id));

        _audit.Action.ShouldBe("ACTIVATE");
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

    private sealed class FakeStatusTracker : IUserStatusTracker
    {
        public List<Guid> InvalidatedUsers { get; } = [];
        public List<Guid> ActivatedUsers { get; } = [];
        public List<Guid> EvictedUsers { get; } = [];

        public void InvalidateUser(Guid userId) => InvalidatedUsers.Add(userId);
        public void ActivateUser(Guid userId) => ActivatedUsers.Add(userId);
        public void EvictUser(Guid userId) => EvictedUsers.Add(userId);
    }

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
        public int RevokeFamilyCalls { get; private set; }
        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(_seeded.Concat(Added).FirstOrDefault(t => t.TokenHash == tokenHash));
        public Task RevokeFamilyAsync(Guid familyId, IClock clock, CancellationToken ct = default)
        {
            RevokeFamilyCalls++;
            foreach (var token in _seeded.Concat(Added).Where(t => t.FamilyId == familyId && t.RevokedAt is null))
            {
                token.Revoke(clock);
            }
            return Task.CompletedTask;
        }
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
        public int CommittedTransactions { get; private set; }

        /// <summary>Simulates losing an optimistic-concurrency race on the next save.</summary>
        public bool FailNextSaveWithConcurrencyConflict { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCalls++;
            if (FailNextSaveWithConcurrencyConflict)
            {
                FailNextSaveWithConcurrencyConflict = false;
                throw new ConcurrencyConflictException();
            }
            return Task.FromResult(0);
        }

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default) =>
            Task.FromResult<ITransactionScope>(new FakeTransaction(this));

        private sealed class FakeTransaction(FakeUnitOfWork owner) : ITransactionScope
        {
            public Task CommitAsync(CancellationToken ct = default)
            {
                owner.CommittedTransactions++;
                return Task.CompletedTask;
            }
            public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
