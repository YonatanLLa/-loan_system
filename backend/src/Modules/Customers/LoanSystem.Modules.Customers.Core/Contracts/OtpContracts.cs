using LoanSystem.Common.Core.Application;

namespace LoanSystem.Modules.Customers.Core.Contracts;

public sealed record OtpIssued(
    Guid OtpRequestId,
    string Channel,
    string MaskedTarget,
    DateTimeOffset ExpiresAt);

public sealed record StartContactVerificationRequest(string Channel);

public sealed record StartContactVerificationCommand(Guid CustomerId, string Channel);

public sealed record ConfirmContactVerificationRequest(Guid OtpRequestId, string Code);

public sealed record ConfirmContactVerificationCommand(
    Guid CustomerId,
    Guid OtpRequestId,
    string Code);

/// <summary>
/// Reusable cross-module OTP service with purpose binding, TTL, cooldown and attempt lock (BE-CUS-11).
/// Used by Customers (contact verification, account change), Loans (contract signature) and Security/Portal (login OTP).
/// </summary>
public interface ICustomerOtpService
{
    Task<Result<OtpIssued>> IssueAsync(
        Guid customerId,
        string purpose,
        Guid? referenceId = null,
        string? preferredChannel = null,
        CancellationToken ct = default);

    Task<Result> VerifyAsync(
        Guid customerId,
        Guid otpRequestId,
        string code,
        string purpose,
        Guid? referenceId = null,
        CancellationToken ct = default);
}
