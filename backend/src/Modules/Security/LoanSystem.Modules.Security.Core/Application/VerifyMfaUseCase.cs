using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class VerifyMfaUseCase : IVerifyMfaUseCase
{
    private static readonly Error InvalidCode =
        Error.Unauthorized("Security.InvalidMfaCode", "The verification code is invalid.");

    private readonly IMfaChallengeRepository _challenges;
    private readonly IMfaRecoveryCodeRepository _recoveryCodes;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITotpService _totp;
    private readonly ISecretProtector _protector;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IClientInfoProvider _clientInfo;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly SecurityOptions _options;

    public VerifyMfaUseCase(
        IMfaChallengeRepository challenges,
        IMfaRecoveryCodeRepository recoveryCodes,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ITotpService totp,
        ISecretProtector protector,
        IJwtTokenGenerator jwt,
        IClientInfoProvider clientInfo,
        ISecurityUnitOfWork unitOfWork,
        IClock clock,
        IAuditContext audit,
        SecurityOptions options)
    {
        _challenges = challenges;
        _recoveryCodes = recoveryCodes;
        _users = users;
        _refreshTokens = refreshTokens;
        _totp = totp;
        _protector = protector;
        _jwt = jwt;
        _clientInfo = clientInfo;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
        _options = options;
    }

    public async Task<Result<AuthTokensResponse>> ExecuteAsync(MfaVerifyRequest request, CancellationToken ct = default)
    {
        var hasTotp = !string.IsNullOrWhiteSpace(request.Code);
        var hasRecovery = !string.IsNullOrWhiteSpace(request.RecoveryCode);
        if (!hasTotp && !hasRecovery)
            return Error.Validation("Security.MissingMfaCode", "A verification code or recovery code is required.");

        var validated = await MfaChallengeValidator.ValidateAsync(
            request.MfaToken, MfaChallengePurpose.VERIFY, _challenges, _users, _clock, ct);
        if (validated.IsFailure)
            return validated.Error;

        var (challenge, user) = validated.Value;
        if (!user.MfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecretEncrypted))
            return MfaChallengeValidator.InvalidChallenge;

        var verified = hasTotp
            ? VerifyTotp(user, request.Code!)
            : await VerifyRecoveryCodeAsync(user.Id, request.RecoveryCode!, ct);

        if (!verified)
        {
            _audit.SetAction("MFA_FAILED");
            _audit.SetActor(user.Id);
            challenge.RegisterFailedAttempt(_options.Mfa.MaxAttemptsPerChallenge, _clock);
            var isNowLocked = user.RegisterFailedLogin(
                _clock, _options.MaxFailedAttempts, TimeSpan.FromMinutes(_options.LockWindowMinutes));

            await _unitOfWork.SaveChangesAsync(ct);
            return isNowLocked ? MfaChallengeValidator.AccountLocked : InvalidCode;
        }

        challenge.Consume(_clock);
        user.RecordSuccessfulLogin(_clock);
        _audit.SetAction("LOGIN");
        _audit.SetActor(user.Id);

        var tokens = TokenIssuer.Issue(
            user, _jwt, _refreshTokens, _clientInfo, _clock, TimeSpan.FromDays(_options.RefreshTokenDays));

        await _unitOfWork.SaveChangesAsync(ct);
        return tokens;
    }

    private bool VerifyTotp(User user, string code)
    {
        var secret = _protector.Unprotect(user.MfaSecretEncrypted!, user.Id);
        if (!_totp.TryVerify(secret, code.Trim(), _clock.UtcNow, out var matchedStep)
            || !user.TryAcceptMfaStep(matchedStep))
        {
            return false;
        }

        if (_protector.NeedsReprotect(user.MfaSecretEncrypted!))
        {
            user.ReprotectMfaSecret(_protector.Protect(secret, user.Id));
        }

        return true;
    }

    private async Task<bool> VerifyRecoveryCodeAsync(Guid userId, string recoveryCode, CancellationToken ct)
    {
        var candidateHash = RecoveryCodeGenerator.Hash(recoveryCode);
        var unused = await _recoveryCodes.FindUnusedAsync(userId, ct);
        var match = unused.FirstOrDefault(c => string.Equals(c.CodeHash, candidateHash, StringComparison.Ordinal));
        if (match is null)
            return false;

        match.Use(_clock);
        return true;
    }
}
