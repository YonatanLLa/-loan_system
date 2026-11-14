using System.Globalization;
using System.Security.Cryptography;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class CustomerOtpService : ICustomerOtpService
{
    private readonly ICustomerRepository _customers;
    private readonly IOtpRepository _otps;
    private readonly IOtpHasher _hasher;
    private readonly IOtpSender _sender;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICustomerAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;
    private readonly IClientInfoProvider _clientInfo;
    private readonly IClock _clock;
    private readonly CustomersOptions _options;

    public CustomerOtpService(
        ICustomerRepository customers,
        IOtpRepository otps,
        IOtpHasher hasher,
        IOtpSender sender,
        ICustomersUnitOfWork unitOfWork,
        ICustomerAuditWriter auditWriter,
        ICurrentUser currentUser,
        IClientInfoProvider clientInfo,
        IClock clock,
        CustomersOptions options)
    {
        _customers = customers;
        _otps = otps;
        _hasher = hasher;
        _sender = sender;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
        _clientInfo = clientInfo;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<OtpIssued>> IssueAsync(
        Guid customerId,
        string purpose,
        Guid? referenceId = null,
        string? preferredChannel = null,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<OtpPurpose>(purpose?.Trim(), ignoreCase: true, out var parsedPurpose))
        {
            return CustomerErrors.Validation($"Invalid OTP purpose '{purpose}'.");
        }

        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var targetResolution = ResolveTarget(customer, parsedPurpose, preferredChannel);
        if (targetResolution.IsFailure)
        {
            return targetResolution.Error;
        }

        var (channel, target) = targetResolution.Value;
        var now = _clock.UtcNow;

        var latest = await _otps.GetLatestByCustomerAndPurposeAsync(customerId, parsedPurpose, ct);
        if (latest is not null && _options.Otp.ResendCooldownSeconds > 0)
        {
            var cooldownTicks = checked(_options.Otp.ResendCooldownSeconds * TimeSpan.TicksPerSecond);
            var elapsedTicks = (now - latest.CreatedAt).Ticks;
            if (elapsedTicks < cooldownTicks)
            {
                var remainingTicks = cooldownTicks - elapsedTicks;
                var retryAfterSeconds = (int)((remainingTicks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
                return CustomerErrors.OtpResendTooSoon(retryAfterSeconds);
            }
        }

        var sentLastHour = await _otps.CountIssuedSinceAsync(
            customerId,
            parsedPurpose,
            now.Subtract(TimeSpanUtils.Hours(1)),
            ct);

        if (sentLastHour >= _options.Otp.MaxSendsPerHour)
        {
            await _auditWriter.WriteAsync(
                "otp_verifications",
                customerId.ToString(),
                "OTP_LOCKED",
                _currentUser.UserId,
                _clientInfo.IpAddress,
                _clientInfo.UserAgent,
                $"{{\"reason\":\"rate_limit\",\"purpose\":\"{parsedPurpose}\"}}",
                ct);
            return CustomerErrors.OtpLocked;
        }

        var activeList = await _otps.FindActiveByCustomerAndPurposeAsync(customerId, parsedPurpose, ct);
        foreach (var prior in activeList)
        {
            prior.Invalidate(_clock);
        }

        var rawCode = GenerateNumericCode(_options.Otp.Length);
        var hash = _hasher.Hash(rawCode);

        var otp = OtpCode.Issue(
            customerId,
            parsedPurpose,
            channel,
            target,
            referenceId,
            hash,
            _clock,
            TimeSpanUtils.Seconds(_options.Otp.TtlSeconds),
            _options.Otp.MaxAttempts,
            _clientInfo.IpAddress);

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);

        _otps.Add(otp);
        await _sender.SendAsync(customerId, channel, target, rawCode, parsedPurpose, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OtpIssued(
            otp.Id,
            channel.ToString(),
            PiiMasker.MaskTarget(channel, target),
            otp.ExpiresAt);
    }

    public async Task<Result> VerifyAsync(
        Guid customerId,
        Guid otpRequestId,
        string code,
        string purpose,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<OtpPurpose>(purpose?.Trim(), ignoreCase: true, out var parsedPurpose))
        {
            return CustomerErrors.Validation($"Invalid OTP purpose '{purpose}'.");
        }

        var otp = await _otps.FindByIdAsync(otpRequestId, ct);
        if (otp is null || otp.CustomerId != customerId || !otp.MatchesScope(parsedPurpose, referenceId))
        {
            await _auditWriter.WriteAsync(
                "otp_verifications",
                otpRequestId.ToString(),
                "OTP_FAILED",
                _currentUser.UserId,
                _clientInfo.IpAddress,
                _clientInfo.UserAgent,
                $"{{\"reason\":\"mismatch_or_missing\",\"purpose\":\"{parsedPurpose}\"}}",
                ct);
            return CustomerErrors.OtpInvalid;
        }

        var (verifyResult, becameLocked) = otp.Verify(code, _hasher, _clock);
        if (verifyResult.IsFailure)
        {
            // Persist attempt count / invalidation outside the caller's later checks so brute-force limits hold
            await _unitOfWork.SaveChangesAsync(ct);

            await _auditWriter.WriteAsync(
                "otp_verifications",
                otp.Id.ToString(),
                becameLocked ? "OTP_LOCKED" : "OTP_FAILED",
                _currentUser.UserId,
                _clientInfo.IpAddress,
                _clientInfo.UserAgent,
                $"{{\"code\":\"{verifyResult.Error.Code}\",\"attempts\":{otp.Attempts},\"purpose\":\"{parsedPurpose}\"}}",
                ct);

            return verifyResult;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static Result<(OtpChannel Channel, string Target)> ResolveTarget(
        Customer customer,
        OtpPurpose purpose,
        string? preferredChannel)
    {
        if (purpose == OtpPurpose.CONTACT_VERIFICATION)
        {
            if (!Enum.TryParse<OtpChannel>(preferredChannel?.Trim(), ignoreCase: true, out var requestedChannel) ||
                requestedChannel is not (OtpChannel.EMAIL or OtpChannel.SMS))
            {
                return CustomerErrors.Validation("Contact verification channel must be EMAIL or SMS.");
            }

            return requestedChannel == OtpChannel.EMAIL
                ? (OtpChannel.EMAIL, customer.Email)
                : (OtpChannel.SMS, customer.PhonePrimary);
        }

        // Security rule (CUS-10.3): sensitive flows only dispatch to previously verified channels on file
        if (customer.PhoneVerifiedAt.HasValue)
        {
            return (OtpChannel.SMS, customer.PhonePrimary);
        }

        if (customer.EmailVerifiedAt.HasValue)
        {
            return (OtpChannel.EMAIL, customer.Email);
        }

        return CustomerErrors.Validation(
            "Customer must have a verified phone number or email address before authorizing this operation.");
    }

    private static string GenerateNumericCode(int length)
    {
        if (length <= 0 || length > 9)
        {
            length = 6;
        }

        var upperBound = 1;
        for (var i = 0; i < length; i++)
        {
            upperBound *= 10;
        }

        var value = RandomNumberGenerator.GetInt32(0, upperBound);
        return value.ToString($"D{length}", CultureInfo.InvariantCulture);
    }
}

internal sealed class StartContactVerificationUseCase : IStartContactVerificationUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerOtpService _otpService;
    private readonly ICurrentUser _currentUser;

    public StartContactVerificationUseCase(
        ICustomerRepository customers,
        ICustomerOtpService otpService,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _otpService = otpService;
        _currentUser = currentUser;
    }

    public async Task<Result<OtpIssued>> ExecuteAsync(StartContactVerificationCommand command, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        if (await _customers.FindByIdAsync(command.CustomerId, ct) is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        return await _otpService.IssueAsync(
            command.CustomerId,
            OtpPurpose.CONTACT_VERIFICATION.ToString(),
            referenceId: null,
            preferredChannel: command.Channel,
            ct);
    }
}

internal sealed class ConfirmContactVerificationUseCase : IConfirmContactVerificationUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly IOtpRepository _otps;
    private readonly ICustomerOtpService _otpService;
    private readonly ICustomersUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public ConfirmContactVerificationUseCase(
        ICustomerRepository customers,
        IOtpRepository otps,
        ICustomerOtpService otpService,
        ICustomersUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IAuditContext audit)
    {
        _customers = customers;
        _otps = otps;
        _otpService = otpService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result> ExecuteAsync(ConfirmContactVerificationCommand command, CancellationToken ct = default)
    {
        if (!CustomerMapper.HasCustomerAccess(
                _currentUser,
                command.CustomerId,
                SecurityPolicies.Admin,
                SecurityPolicies.Analyst,
                SecurityPolicies.Operator))
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var customer = await _customers.FindByIdAsync(command.CustomerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(command.CustomerId);
        }

        var verifyResult = await _otpService.VerifyAsync(
            command.CustomerId,
            command.OtpRequestId,
            command.Code,
            OtpPurpose.CONTACT_VERIFICATION.ToString(),
            referenceId: null,
            ct);

        if (verifyResult.IsFailure)
        {
            return verifyResult;
        }

        var otp = await _otps.FindByIdAsync(command.OtpRequestId, ct);
        if (otp is null)
        {
            return CustomerErrors.OtpInvalid;
        }

        _audit.SetAction("VERIFY_CONTACT", "customers");
        if (otp.Channel == OtpChannel.EMAIL)
        {
            customer.MarkEmailVerified(_clock);
        }
        else
        {
            customer.MarkPhoneVerified(_clock);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
