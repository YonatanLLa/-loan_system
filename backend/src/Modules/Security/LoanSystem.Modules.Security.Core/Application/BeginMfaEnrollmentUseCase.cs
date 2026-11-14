using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Core.Application;

internal sealed class BeginMfaEnrollmentUseCase : IBeginMfaEnrollmentUseCase
{
    private readonly IMfaChallengeRepository _challenges;
    private readonly IUserRepository _users;
    private readonly ITotpService _totp;
    private readonly ISecretProtector _protector;
    private readonly ISecurityUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public BeginMfaEnrollmentUseCase(
        IMfaChallengeRepository challenges,
        IUserRepository users,
        ITotpService totp,
        ISecretProtector protector,
        ISecurityUnitOfWork unitOfWork,
        IClock clock)
    {
        _challenges = challenges;
        _users = users;
        _totp = totp;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<MfaEnrollResponse>> ExecuteAsync(MfaEnrollRequest request, CancellationToken ct = default)
    {
        var validated = await MfaChallengeValidator.ValidateAsync(
            request.MfaToken, MfaChallengePurpose.ENROLL, _challenges, _users, _clock, ct);
        if (validated.IsFailure)
            return validated.Error;

        var (_, user) = validated.Value;
        if (user.MfaEnabled)
            return Error.Conflict("Security.MfaAlreadyEnabled", "MFA is already enabled for this user.");

        var secret = _totp.GenerateSecret();
        var encryptedSecret = _protector.Protect(secret, user.Id);
        user.StartMfaEnrollment(encryptedSecret);

        await _unitOfWork.SaveChangesAsync(ct);

        var otpauthUri = _totp.BuildProvisioningUri(secret, user.Email);
        return new MfaEnrollResponse(secret, otpauthUri);
    }
}
