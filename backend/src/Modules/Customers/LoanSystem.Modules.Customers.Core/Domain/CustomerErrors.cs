using LoanSystem.Common.Core.Application;

namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>Catalog of all domain and application errors returned by the Customers module (§8).</summary>
internal static class CustomerErrors
{
    public static Error NotFound(Guid customerId) =>
        Error.NotFound("Customers.NotFound", $"Customer '{customerId}' was not found.");

    public static Error NotFound() =>
        Error.NotFound("Customers.NotFound", "Customer was not found.");

    public static Error InvalidDocumentNumber(string reason) =>
        Error.Validation("Customers.InvalidDocumentNumber", reason);

    public static Error DocumentTypeInactive(string docType) =>
        Error.Validation("Customers.DocumentTypeInactive", $"Document type '{docType}' is inactive or unavailable.");

    public static Error Underage =>
        Error.Validation("Customers.Underage", "Natural person customer must be at least 18 years old.");

    public static Error InvalidPersonDocumentCombination(string message) =>
        Error.Validation("Customers.InvalidPersonDocumentCombination", message);

    public static Error DocumentAlreadyExists =>
        Error.Conflict("Customers.DocumentAlreadyExists", "A customer with this document type and number already exists.");

    public static Error ConcurrencyConflict =>
        Error.Conflict("Customers.ConcurrencyConflict", "The customer record was modified concurrently. Refresh and retry.");

    public static Error Validation(string message) =>
        Error.Validation("Customers.Validation", message);

    public static Error FileTooLarge(int maxFileSizeMb) =>
        Error.Validation("Customers.FileTooLarge", $"File exceeds the maximum allowed size of {maxFileSizeMb} MB.");

    public static Error UnsupportedFileType =>
        Error.Validation("Customers.UnsupportedFileType", "Unsupported or invalid file content type.");

    public static Error DocumentNotFound(Guid documentId) =>
        Error.NotFound("Customers.DocumentNotFound", $"Document '{documentId}' was not found.");

    public static Error InvalidKycTransition(string message) =>
        Error.Conflict("Customers.InvalidKycTransition", message);

    public static Error KycNotesRequired(int minLength) =>
        Error.Unprocessable("Customers.KycNotesRequired", $"Decision notes/reason must be at least {minLength} characters.");

    public static Error MissingRequiredDocuments(IReadOnlyList<string> missing) =>
        Error.Unprocessable("Customers.MissingRequiredDocuments", "Required KYC documents are missing.")
            .WithExtension("missing", missing);

    public static Error KycRequiresAdmin =>
        Error.Forbidden("Customers.KycRequiresAdmin", "Only an administrator can perform this KYC action for PEP, high-risk, or already verified customers.");

    public static Error KycCheckFailed =>
        Error.Unprocessable("Customers.KycCheckFailed", "Cannot approve KYC while an automatic verification check is in FAIL status.");

    public static Error ConsentRequired(string purpose) =>
        Error.Unprocessable("Customers.ConsentRequired", $"Required consent '{purpose}' must be accepted.");

    public static Error ConsentVersionOutdated(string purpose, string expectedVersion) =>
        Error.Unprocessable("Customers.ConsentVersionOutdated", $"Consent for '{purpose}' must reference current version '{expectedVersion}'.");

    public static Error ConsentNotRevocable(string purpose) =>
        Error.Unprocessable("Customers.ConsentNotRevocable", $"Consent '{purpose}' cannot be revoked via this endpoint in MVP.");

    public static Error InvalidCci(string message) =>
        Error.Validation("Customers.InvalidCci", message);

    public static Error InvalidWalletPhone =>
        Error.Validation("Customers.InvalidWalletPhone", "Digital wallet phone must be a valid 9-digit Peruvian mobile number starting with 9.");

    public static Error RawCardNotAllowed =>
        Error.Validation("Customers.RawCardNotAllowed", "Raw card numbers (PAN) are rejected; provide a payment gateway token.");

    public static Error BankAccountAlreadyExists =>
        Error.Conflict("Customers.BankAccountAlreadyExists", "This bank account is already registered for the customer.");

    public static Error BankAccountLimitReached(int maxActive) =>
        Error.Unprocessable("Customers.BankAccountLimitReached", $"Customer reached the maximum of {maxActive} active or pending bank accounts.");

    public static Error AccountHolderMismatch =>
        Error.Unprocessable("Customers.AccountHolderMismatch", "Declared account holder name does not match the customer's full name.");

    public static Error AccountNotVerified =>
        Error.Unprocessable("Customers.AccountNotVerified", "Only active and verified bank accounts can be set as primary.");

    public static Error BankAccountNotFound(Guid accountId) =>
        Error.NotFound("Customers.BankAccountNotFound", $"Bank account '{accountId}' was not found.");

    public static Error OtpInvalid =>
        Error.Unprocessable("Customers.OtpInvalid", "The verification code is invalid.");

    public static Error OtpExpired =>
        Error.Unprocessable("Customers.OtpExpired", "The verification code has expired.");

    public static Error OtpAlreadyUsed =>
        Error.Unprocessable("Customers.OtpAlreadyUsed", "The verification code has already been used.");

    public static Error OtpResendTooSoon(int retryAfterSeconds) =>
        Error.Unprocessable("Customers.OtpResendTooSoon", $"Please wait {retryAfterSeconds} seconds before requesting another verification code.")
            .WithExtension("retryAfterSeconds", retryAfterSeconds);

    public static Error OtpLocked =>
        Error.Locked("Customers.OtpLocked", "Too many failed OTP attempts or hourly sends reached. Please try again later.");
}
