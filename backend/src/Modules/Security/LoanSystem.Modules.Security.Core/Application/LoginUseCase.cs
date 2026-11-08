using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

// TODO(auth-hardening): pendientes de seguridad a revisar al terminar el módulo de auth.
//  1. [ALTA] Ataque de tiempo / enumeración de emails: si el usuario no existe se responde
//     en ~5 ms (sin bcrypt) y si existe en ~250 ms. Aunque el mensaje sea el mismo, el tiempo
//     delata qué emails están registrados. Solución: cuando user es null, verificar igual
//     contra un hash ficticio (dummy BCrypt hash) para igualar el tiempo de respuesta.
//  2. [MEDIA] Límite de 72 bytes de BCrypt: BCrypt ignora todo lo que pasa del byte 72.
//     Validar largo máximo de contraseña (ej. 64 chars) al registrar / cambiar / resetear
//     contraseña (ver backend/docs/PLAN_02_EMAIL_VERIFICATION_Y_RESET.md).
//  3. [BAJA] Re-hash al subir el WorkFactor: en login exitoso, si
//     BCrypt.PasswordNeedsRehash(hash, WorkFactor) es true, re-hashear y guardar.
//     Requiere exponer un NeedsRehash(...) en IPasswordHasher (BcryptPasswordHasher).
//  4. [BAJA] IsLockedOut se evalúa antes de verificar la contraseña: revela que el email
//     existe y está bloqueado sin conocer la contraseña. Evaluar si se acepta el riesgo.
//  5. [BAJA] Constantes duplicadas: DefaultMaxFailedAttempts / DefaultLockWindowMinutes
//     también están en SecurityModule. Unificar en una clase SecurityOptions con IOptions<T>.
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IClientInfoProvider _clientInfo;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly TimeSpan _refreshLifetime;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockWindow;

    public LoginUseCase(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        IClientInfoProvider clientInfo,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        int refreshTokenDays,
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        int lockWindowMinutes = DefaultLockWindowMinutes)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _clientInfo = clientInfo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _refreshLifetime = TimeSpan.FromDays(refreshTokenDays);
        _maxFailedAttempts = maxFailedAttempts;
        _lockWindow = TimeSpan.FromMinutes(lockWindowMinutes);
    }

    public async Task<Result<AuthTokensResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
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
            var isNowLocked = user.RegisterFailedLogin(_clock, _maxFailedAttempts, _lockWindow);
            await _unitOfWork.SaveChangesAsync(ct);
            return isNowLocked ? AccountLocked : InvalidCredentials;
        }

        if (user.Status == UserStatus.INACTIVE)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        // TODO(auth-hardening) #3: re-hashear la contraseña si el WorkFactor cambió.
        user.RecordSuccessfulLogin(_clock);
        var tokens = TokenIssuer.Issue(user, _jwt, _refreshTokens, _clientInfo, _clock, _refreshLifetime);

        await _unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }
}
