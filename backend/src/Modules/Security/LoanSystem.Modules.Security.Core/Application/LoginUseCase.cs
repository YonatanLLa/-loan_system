using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

// TODO(auth-hardening): pendientes de seguridad a revisar al terminar el módulo de auth.
//  1. [ALTA] Ataque de tiempo / enumeración de emails: cuando user es null, verificar contra hash ficticio.
//  2. [MEDIA] Límite de 72 bytes de BCrypt: validar largo máximo de contraseña (ej. 64 chars).
//  3. [BAJA] Re-hash al subir el WorkFactor.
//  4. [BAJA] IsLockedOut se evalúa antes de verificar la contraseña.
internal sealed class LoginUseCase : ILoginUseCase
{
    private const int DefaultMaxFailedAttempts = 5;
    private const int DefaultLockWindowMinutes = 15;

    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Security.InvalidCredentials", "Email or password is incorrect.");

    private static readonly Error AccountLocked =
        Error.Locked("Security.AccountLocked", "The user account is temporarily locked due to too many failed login attempts.");

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IMfaChallengeRepository? _mfaChallenges;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IClientInfoProvider _clientInfo;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly TimeSpan _refreshLifetime;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockWindow;
    private readonly MfaOptions _mfaOptions;

    public LoginUseCase(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        IClientInfoProvider clientInfo,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IAuditContext audit,
        int refreshTokenDays,
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        int lockWindowMinutes = DefaultLockWindowMinutes,
        IMfaChallengeRepository? mfaChallenges = null,
        MfaOptions? mfaOptions = null)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _mfaChallenges = mfaChallenges;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _clientInfo = clientInfo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _refreshLifetime = TimeSpan.FromDays(refreshTokenDays);
        _maxFailedAttempts = maxFailedAttempts;
        _lockWindow = TimeSpan.FromMinutes(lockWindowMinutes);
        _mfaOptions = mfaOptions ?? new MfaOptions();
    }

    public async Task<Result<LoginResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Error.Validation("Security.MissingCredentials", "Email and password are required.");

        var user = await _users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null)
            return InvalidCredentials; // TODO(auth-hardening) #1: igualar tiempo con hash ficticio.

        if (user.IsLockedOut(_clock)) // TODO(auth-hardening) #4: revela cuenta existente y bloqueada.
            return AccountLocked;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Sin actor: quien falla la contraseña no está identificado (BE-SEC-07)
            _audit.SetAction("LOGIN_FAILED");
            var isNowLocked = user.RegisterFailedLogin(_clock, _maxFailedAttempts, _lockWindow);
            await _unitOfWork.SaveChangesAsync(ct);
            return isNowLocked ? AccountLocked : InvalidCredentials;
        }

        if (user.Status == UserStatus.INACTIVE)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        if (user.RequiresMfa(_mfaOptions.RequiredRoles))
        {
            if (_mfaChallenges is null)
                throw new InvalidOperationException("IMfaChallengeRepository must be configured for MFA-required roles.");

            var purpose = user.MfaEnabled ? MfaChallengePurpose.VERIFY : MfaChallengePurpose.ENROLL;
            var lifetimeMinutes = user.MfaEnabled
                ? _mfaOptions.ChallengeMinutes
                : _mfaOptions.EnrollmentChallengeMinutes;

            var (rawToken, tokenHash) = RefreshTokenHasher.Create();
            var challenge = MfaChallenge.Issue(
                user.Id,
                tokenHash,
                purpose,
                _clock,
                TimeSpan.FromMinutes(lifetimeMinutes),
                _clientInfo.IpAddress,
                _clientInfo.UserAgent);

            _mfaChallenges.Add(challenge);
            // Do NOT reset failed-login counter until the second factor succeeds (BE-SEC-03).
            await _unitOfWork.SaveChangesAsync(ct);

            var status = user.MfaEnabled ? "MFA_REQUIRED" : "MFA_ENROLLMENT_REQUIRED";
            return LoginResponse.Challenge(status, rawToken, lifetimeMinutes * 60);
        }

        // TODO(auth-hardening) #3: re-hashear la contraseña si el WorkFactor cambió.
        user.RecordSuccessfulLogin(_clock);
        // La petición es anónima: se atribuye el cambio al usuario que inicia sesión (BE-SEC-07)
        _audit.SetAction("LOGIN");
        _audit.SetActor(user.Id);
        var tokens = TokenIssuer.Issue(user, _jwt, _refreshTokens, _clientInfo, _clock, _refreshLifetime);

        await _unitOfWork.SaveChangesAsync(ct);
        return LoginResponse.Authenticated(tokens);
    }
}
