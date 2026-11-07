using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

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
            return InvalidCredentials;

        if (user.IsLockedOut(_clock))
            return AccountLocked;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var isNowLocked = user.RegisterFailedLogin(_clock, _maxFailedAttempts, _lockWindow);
            await _unitOfWork.SaveChangesAsync(ct);
            return isNowLocked ? AccountLocked : InvalidCredentials;
        }

        if (user.Status == UserStatus.INACTIVE)
            return Error.Forbidden("Security.UserNotActive", "The user account is not active.");

        user.RecordSuccessfulLogin(_clock);
        var tokens = TokenIssuer.Issue(user, _jwt, _refreshTokens, _clientInfo, _clock, _refreshLifetime);

        await _unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }
}
