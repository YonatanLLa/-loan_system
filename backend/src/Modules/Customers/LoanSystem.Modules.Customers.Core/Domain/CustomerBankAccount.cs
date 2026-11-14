using System.Text.RegularExpressions;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Domain.Validators;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>
/// Customer bank account, digital wallet, or tokenized debit card (BE-CUS-09, BE-CUS-10).
/// Plaintext account numbers/CCI are never persisted or exposed in API responses (D4, RNF-05).
/// </summary>
internal sealed partial class CustomerBankAccount : Entity<Guid>
{
    public const int MaxVerificationNotesLength = 500;
    public const int MaxBankNameLength = 100;

    public Guid CustomerId { get; private set; }
    public string BankName { get; private set; } = string.Empty;
    public string? BankCode { get; private set; }
    public BankAccountType AccountType { get; private set; }

    // Legacy masked column retained as ****XXXX for DB compatibility; CCI column stays null
    public string AccountNumberMasked { get; private set; } = string.Empty;
    public string? AccountNumberEncrypted { get; private set; }
    public string? AccountNumberHash { get; private set; }
    public string Last4 { get; private set; } = string.Empty;
    public string? PhoneWallet { get; private set; }
    public string? HolderName { get; private set; }
    public string? GatewayToken { get; private set; }

    public bool IsVerified { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public string? VerificationMethod { get; private set; }
    public string? VerificationNotes { get; private set; }

    public bool IsPrimary { get; private set; }
    public BankAccountStatus Status { get; private set; }
    public DateTimeOffset? DisbursementEnabledFrom { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private CustomerBankAccount() { }

    public static Result<CustomerBankAccount> Register(
        Guid customerId,
        BankAccountType accountType,
        string? rawBankName,
        string? rawCci,
        string? rawPhoneWallet,
        string? rawGatewayToken,
        string? declaredHolderName,
        ICciValidator cciValidator,
        IFieldProtector protector,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(cciValidator);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.UtcNow;

        if (accountType is BankAccountType.CUENTA_AHORRO or BankAccountType.CUENTA_CORRIENTE)
        {
            var cciResult = cciValidator.Validate(rawCci);
            if (cciResult.IsFailure)
            {
                return cciResult.Error;
            }

            var validated = cciResult.Value;
            var bankName = string.IsNullOrWhiteSpace(rawBankName)
                ? validated.BankName
                : rawBankName.Trim();
            if (bankName.Length > MaxBankNameLength)
            {
                return CustomerErrors.Validation($"Bank name must not exceed {MaxBankNameLength} characters.");
            }

            return new CustomerBankAccount
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                BankName = bankName,
                BankCode = validated.BankCode,
                AccountType = accountType,
                AccountNumberMasked = $"****{validated.Last4}",
                AccountNumberEncrypted = protector.Protect(validated.NormalizedCci),
                AccountNumberHash = protector.Hash(validated.NormalizedCci),
                Last4 = validated.Last4,
                PhoneWallet = null,
                HolderName = NormalizeOptional(declaredHolderName, 200),
                GatewayToken = null,
                IsVerified = false,
                IsPrimary = false,
                Status = BankAccountStatus.PENDING_VERIFICATION,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        if (accountType == BankAccountType.BILLETERA_DIGITAL)
        {
            var phone = ContactInfo.NormalizePeruvianMobile(rawPhoneWallet);
            if (phone is null)
            {
                return CustomerErrors.InvalidWalletPhone;
            }

            if (string.IsNullOrWhiteSpace(rawBankName))
            {
                return CustomerErrors.Validation("Wallet provider name (e.g., Yape, Plin) is required.");
            }

            var bankName = rawBankName.Trim();
            if (bankName.Length > MaxBankNameLength)
            {
                return CustomerErrors.Validation($"Bank name must not exceed {MaxBankNameLength} characters.");
            }

            var last4 = phone[^4..];
            return new CustomerBankAccount
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                BankName = bankName,
                BankCode = null,
                AccountType = BankAccountType.BILLETERA_DIGITAL,
                AccountNumberMasked = $"*****{last4}",
                AccountNumberEncrypted = protector.Protect(phone),
                AccountNumberHash = protector.Hash($"WALLET:{phone}"),
                Last4 = last4,
                PhoneWallet = phone,
                HolderName = NormalizeOptional(declaredHolderName, 200),
                GatewayToken = null,
                IsVerified = false,
                IsPrimary = false,
                Status = BankAccountStatus.PENDING_VERIFICATION,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        if (accountType == BankAccountType.TARJETA_DEBITO)
        {
            if (LooksLikeRawPan(rawGatewayToken) || LooksLikeRawPan(rawCci))
            {
                return CustomerErrors.RawCardNotAllowed;
            }

            if (string.IsNullOrWhiteSpace(rawGatewayToken))
            {
                return CustomerErrors.Validation("Gateway token is required for debit card accounts.");
            }

            if (string.IsNullOrWhiteSpace(rawBankName))
            {
                return CustomerErrors.Validation("Issuing bank name is required.");
            }

            var token = rawGatewayToken.Trim();
            if (token.Length < 8 || token.Length > 200)
            {
                return CustomerErrors.Validation("Gateway token length must be between 8 and 200 characters.");
            }

            var bankName = rawBankName.Trim();
            if (bankName.Length > MaxBankNameLength)
            {
                return CustomerErrors.Validation($"Bank name must not exceed {MaxBankNameLength} characters.");
            }

            var last4 = token[^4..];
            return new CustomerBankAccount
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                BankName = bankName,
                BankCode = null,
                AccountType = BankAccountType.TARJETA_DEBITO,
                AccountNumberMasked = $"****{last4}",
                AccountNumberEncrypted = null,
                AccountNumberHash = protector.Hash($"CARD_TOKEN:{token}"),
                Last4 = last4,
                PhoneWallet = null,
                HolderName = NormalizeOptional(declaredHolderName, 200),
                GatewayToken = token,
                IsVerified = false,
                IsPrimary = false,
                Status = BankAccountStatus.PENDING_VERIFICATION,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        return CustomerErrors.Validation($"Unsupported account type '{accountType}'.");
    }

    public Result VerifyOwnership(
        Guid verifierUserId,
        string? declaredHolderName,
        string customerFullName,
        string method,
        string? notes,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status == BankAccountStatus.INACTIVE)
        {
            return Error.Conflict("Customers.BankAccountInactive", "Cannot verify an inactive bank account.");
        }

        var normalizedMethod = string.IsNullOrWhiteSpace(method) ? "MANUAL" : method.Trim().ToUpperInvariant();
        if (normalizedMethod is not ("MANUAL" or "PENNY_TEST"))
        {
            return CustomerErrors.Validation("Unsupported ownership verification method.");
        }

        var left = PersonName.NormalizeForComparison(declaredHolderName);
        var right = PersonName.NormalizeForComparison(customerFullName);
        if (string.IsNullOrEmpty(left) || !string.Equals(left, right, StringComparison.Ordinal))
        {
            return CustomerErrors.AccountHolderMismatch;
        }

        HolderName = left;
        IsVerified = true;
        VerifiedBy = verifierUserId == Guid.Empty ? null : verifierUserId;
        VerifiedAt = clock.UtcNow;
        VerificationMethod = normalizedMethod;
        VerificationNotes = NormalizeOptional(notes, MaxVerificationNotesLength);
        Status = BankAccountStatus.ACTIVE;
        UpdatedAt = clock.UtcNow;

        return Result.Success();
    }

    public Result SetAsFirstPrimary(IClock clock)
    {
        if (Status != BankAccountStatus.ACTIVE || !IsVerified)
        {
            return CustomerErrors.AccountNotVerified;
        }

        IsPrimary = true;
        DisbursementEnabledFrom = null;
        UpdatedAt = clock.UtcNow;
        return Result.Success();
    }

    public Result PromoteToPrimaryWithCooling(TimeSpan coolingPeriod, IClock clock)
    {
        if (Status != BankAccountStatus.ACTIVE || !IsVerified)
        {
            return CustomerErrors.AccountNotVerified;
        }

        IsPrimary = true;
        DisbursementEnabledFrom = clock.UtcNow.Add(coolingPeriod);
        UpdatedAt = clock.UtcNow;
        return Result.Success();
    }

    public void ClearPrimary(IClock clock)
    {
        if (!IsPrimary)
        {
            return;
        }

        IsPrimary = false;
        UpdatedAt = clock.UtcNow;
    }

    public void Deactivate(IClock clock)
    {
        if (Status == BankAccountStatus.INACTIVE)
        {
            return;
        }

        Status = BankAccountStatus.INACTIVE;
        IsPrimary = false;
        DeactivatedAt = clock.UtcNow;
        UpdatedAt = clock.UtcNow;
    }

    private static bool LooksLikeRawPan(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = NonDigitStripRegex().Replace(input.Trim(), string.Empty);
        return RawPanDigitsRegex().IsMatch(compact);
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLen ? trimmed[..maxLen] : trimmed;
    }

    [GeneratedRegex(@"[\s-]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonDigitStripRegex();

    [GeneratedRegex(@"^\d{13,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex RawPanDigitsRegex();
}
