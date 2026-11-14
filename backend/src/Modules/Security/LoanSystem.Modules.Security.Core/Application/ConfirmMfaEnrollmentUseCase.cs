using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class ConfirmMfaEnrollmentUseCase : IConfirmMfaEnrollmentUseCase
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

    public ConfirmMfaEnrollmentUseCase(
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

    public async Task<Result<MfaEnrollConfirmResponse>> ExecuteAsync(
        MfaEnrollConfirmRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Error.Validation("Security.MissingMfaCode", "A 6-digit verification code is required.");

        var validated = await MfaChallengeValidator.ValidateAsync(
            request.MfaToken, MfaChallengePurpose.ENROLL, _challenges, _users, _clock, ct);
        if (validated.IsFailure)
            return validated.Error;

        var (challenge, user) = validated.Value;
        if (user.MfaEnabled)
            return Error.Conflict("Security.MfaAlreadyEnabled", "MFA is already enabled for this user.");

        if (string.IsNullOrWhiteSpace(user.MfaSecretEncrypted))
            return Error.Validation("Security.MfaEnrollmentNotStarted", "MFA enrollment has not been initiated.");

        var secret = _protector.Unprotect(user.MfaSecretEncrypted, user.Id);
        if (!_totp.TryVerify(secret, request.Code.Trim(), _clock.UtcNow, out var matchedStep))
        {
            _audit.SetAction("MFA_ENROLL_FAILED");
            _audit.SetActor(user.Id);
            challenge.RegisterFailedAttempt(_options.Mfa.MaxAttemptsPerChallenge, _clock);
            var isNowLocked = user.RegisterFailedLogin(
                _clock, _options.MaxFailedAttempts, TimeSpan.FromMinutes(_options.LockWindowMinutes));

            await _unitOfWork.SaveChangesAsync(ct);
            return isNowLocked ? MfaChallengeValidator.AccountLocked : InvalidCode;
        }

        if (_protector.NeedsReprotect(user.MfaSecretEncrypted))
        {
            user.ReprotectMfaSecret(_protector.Protect(secret, user.Id));
        }

        user.CompleteMfaEnrollment(_clock, matchedStep);

        var rawRecoveryCodes = RecoveryCodeGenerator.GenerateBatch();
        var recoveryEntities = rawRecoveryCodes
            .Select(code => MfaRecoveryCode.Create(user.Id, RecoveryCodeGenerator.Hash(code), _clock));
        _recoveryCodes.AddRange(recoveryEntities);

        challenge.Consume(_clock);
        user.RecordSuccessfulLogin(_clock);
        _audit.SetAction("MFA_ENROLLED");
        _audit.SetActor(user.Id);

        var tokens = TokenIssuer.Issue(
            user, _jwt, _refreshTokens, _clientInfo, _clock, TimeSpan.FromDays(_options.RefreshTokenDays));

        await _unitOfWork.SaveChangesAsync(ct);
        return new MfaEnrollConfirmResponse(tokens, rawRecoveryCodes);
    }
}
