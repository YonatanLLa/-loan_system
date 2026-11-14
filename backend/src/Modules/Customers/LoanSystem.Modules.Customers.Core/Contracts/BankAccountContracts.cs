namespace LoanSystem.Modules.Customers.Core.Contracts;

public sealed record RegisterBankAccountRequest(
    string BankName,
    string AccountType,
    string? Cci,
    string? PhoneWallet,
    string? GatewayToken,
    string? HolderName = null);

public sealed record RegisterBankAccountCommand(
    Guid CustomerId,
    RegisterBankAccountRequest Request);

public sealed record VerifyBankAccountRequest(
    string HolderName,
    string Method = "MANUAL",
    string? Notes = null);

public sealed record VerifyBankAccountCommand(
    Guid CustomerId,
    Guid AccountId,
    VerifyBankAccountRequest Request);

public sealed record DeactivateBankAccountCommand(Guid CustomerId, Guid AccountId);

public sealed record SetPrimaryBankAccountCommand(Guid CustomerId, Guid AccountId);

public sealed record ConfirmPrimaryBankAccountRequest(Guid OtpRequestId, string Code);

public sealed record ConfirmPrimaryBankAccountCommand(
    Guid CustomerId,
    Guid AccountId,
    Guid OtpRequestId,
    string Code);

public sealed record BankAccountResponse(
    Guid Id,
    string BankName,
    string? BankCode,
    string AccountType,
    string Last4,
    string? PhoneWalletMasked,
    bool IsVerified,
    bool IsPrimary,
    string Status,
    DateTimeOffset? DisbursementEnabledFrom);

public sealed record SetPrimaryBankAccountResult(
    bool RequiresOtp,
    BankAccountResponse? Account,
    OtpIssued? OtpChallenge);
