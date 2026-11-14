using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

/// <summary>
/// Customer aggregate root (natural person or legal entity) managing identity invariants,
/// profile updates, contact verification state, and the KYC lifecycle (BE-CUS-01..05).
/// </summary>
internal sealed class Customer : AggregateRoot<Guid>
{
    public const int MinNaturalAgeYears = 18;
    public const int StandardApprovalNotesMinLength = 10;
    public const int ReviewOrErrorApprovalNotesMinLength = 30;
    public const int MaxAddressLength = 500;
    public const int MaxDistrictLength = 100;
    public const int MaxNotesLength = 500;

    public PersonType PersonType { get; private set; }
    public IdentityDocument Document { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public DateOnly? BirthDate { get; private set; }

    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public string PhonePrimary { get; private set; } = string.Empty;
    public string? PhoneSecondary { get; private set; }
    public DateTimeOffset? PhoneVerifiedAt { get; private set; }

    public string Address { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;

    public Money MonthlyNetIncome { get; private set; } = Money.Zero();
    public string? Occupation { get; private set; }
    public string? EmployerName { get; private set; }
    public int WorkSeniorityMonths { get; private set; }

    public KycStatus KycStatus { get; private set; }
    public DateTimeOffset? KycSubmittedAt { get; private set; }
    public DateTimeOffset? KycVerifiedAt { get; private set; }
    public Guid? KycVerifiedBy { get; private set; }
    public DateTimeOffset? KycExpiresAt { get; private set; }
    public string? KycRejectedReason { get; private set; }
    public string? KycNotes { get; private set; }

    public bool IsPep { get; private set; }
    public AmlRiskLevel AmlRiskLevel { get; private set; }
    public decimal CreditBalance { get; private set; }
    public Guid? PortalUserId { get; private set; }

    public int Version { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Customer() { }

    public static Result<Customer> RegisterNatural(
        IdentityDocument document,
        PersonName name,
        DateOnly? birthDate,
        ContactInfo contact,
        string? address,
        string? district,
        SocioeconomicProfile profile,
        Guid createdBy,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(clock);

        if (document.Type is not (DocumentType.DNI or DocumentType.CE or DocumentType.PASAPORTE))
        {
            return CustomerErrors.InvalidPersonDocumentCombination(
                $"Natural person must use DNI, CE, or PASAPORTE, not '{document.Type}'.");
        }

        if (!birthDate.HasValue)
        {
            return CustomerErrors.Validation("Birth date is required for natural persons.");
        }

        var age = CalculateAge(birthDate.Value, clock.LimaToday);
        if (age < MinNaturalAgeYears)
        {
            return CustomerErrors.Underage;
        }

        var addressResult = ValidateAddress(address, district);
        if (addressResult.IsFailure)
        {
            return addressResult.Error;
        }

        var (normalizedAddress, normalizedDistrict) = addressResult.Value;
        var now = clock.UtcNow;

        return new Customer
        {
            Id = Guid.NewGuid(),
            PersonType = PersonType.NATURAL,
            Document = document,
            FullName = name.Value,
            BirthDate = birthDate,
            Email = contact.Email,
            PhonePrimary = contact.PhonePrimary,
            PhoneSecondary = contact.PhoneSecondary,
            Address = normalizedAddress,
            District = normalizedDistrict,
            MonthlyNetIncome = profile.MonthlyNetIncome,
            Occupation = profile.Occupation,
            EmployerName = profile.EmployerName,
            WorkSeniorityMonths = profile.WorkSeniorityMonths,
            KycStatus = KycStatus.PENDING,
            AmlRiskLevel = AmlRiskLevel.LOW,
            IsPep = false,
            CreditBalance = 0m,
            Version = 0,
            CreatedBy = createdBy == Guid.Empty ? null : createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static Result<Customer> RegisterLegalEntity(
        IdentityDocument ruc,
        PersonName businessName,
        DateOnly? birthDate,
        ContactInfo contact,
        string? address,
        string? district,
        SocioeconomicProfile profile,
        Guid createdBy,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(ruc);
        ArgumentNullException.ThrowIfNull(businessName);
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(clock);

        if (ruc.Type != DocumentType.RUC || !ruc.Number.StartsWith("20", StringComparison.Ordinal))
        {
            return CustomerErrors.InvalidPersonDocumentCombination(
                "Legal entity (JURIDICA) must register with an 11-digit RUC starting with '20'.");
        }

        if (birthDate.HasValue)
        {
            return CustomerErrors.InvalidPersonDocumentCombination(
                "Legal entity must not specify a birth date.");
        }

        var addressResult = ValidateAddress(address, district);
        if (addressResult.IsFailure)
        {
            return addressResult.Error;
        }

        var (normalizedAddress, normalizedDistrict) = addressResult.Value;
        var now = clock.UtcNow;

        return new Customer
        {
            Id = Guid.NewGuid(),
            PersonType = PersonType.JURIDICA,
            Document = ruc,
            FullName = businessName.Value,
            BirthDate = null,
            Email = contact.Email,
            PhonePrimary = contact.PhonePrimary,
            PhoneSecondary = contact.PhoneSecondary,
            Address = normalizedAddress,
            District = normalizedDistrict,
            MonthlyNetIncome = profile.MonthlyNetIncome,
            Occupation = profile.Occupation,
            EmployerName = profile.EmployerName,
            WorkSeniorityMonths = profile.WorkSeniorityMonths,
            KycStatus = KycStatus.PENDING,
            AmlRiskLevel = AmlRiskLevel.LOW,
            IsPep = false,
            CreditBalance = 0m,
            Version = 0,
            CreatedBy = createdBy == Guid.Empty ? null : createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public IReadOnlyList<string> UpdateContact(ContactInfo next, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(clock);

        var criticalChanges = new List<string>();

        if (!string.Equals(Email, next.Email, StringComparison.Ordinal))
        {
            Email = next.Email;
            EmailVerifiedAt = null;
            criticalChanges.Add("email");
        }

        if (!string.Equals(PhonePrimary, next.PhonePrimary, StringComparison.Ordinal))
        {
            PhonePrimary = next.PhonePrimary;
            PhoneVerifiedAt = null;
            criticalChanges.Add("phone_primary");
        }

        if (!string.Equals(PhoneSecondary, next.PhoneSecondary, StringComparison.Ordinal))
        {
            PhoneSecondary = next.PhoneSecondary;
            criticalChanges.Add("phone_secondary");
        }

        if (criticalChanges.Count > 0)
        {
            Touch(clock);
        }

        return criticalChanges;
    }

    public Result<IReadOnlyList<string>> UpdateAddress(string? address, string? district, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var validated = ValidateAddress(address, district);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var (nextAddress, nextDistrict) = validated.Value;
        var criticalChanges = new List<string>();

        if (!string.Equals(Address, nextAddress, StringComparison.Ordinal))
        {
            Address = nextAddress;
            criticalChanges.Add("address");
        }

        if (!string.Equals(District, nextDistrict, StringComparison.Ordinal))
        {
            District = nextDistrict;
            criticalChanges.Add("district");
        }

        if (criticalChanges.Count > 0)
        {
            Touch(clock);
        }

        return criticalChanges;
    }

    public IReadOnlyList<string> UpdateSocioeconomicProfile(SocioeconomicProfile next, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(clock);

        var criticalChanges = new List<string>();
        var anyChanged = false;

        if (MonthlyNetIncome.Amount != next.MonthlyNetIncome.Amount)
        {
            MonthlyNetIncome = next.MonthlyNetIncome;
            criticalChanges.Add("monthly_net_income");
            anyChanged = true;
        }

        if (!string.Equals(Occupation, next.Occupation, StringComparison.Ordinal))
        {
            Occupation = next.Occupation;
            anyChanged = true;
        }

        if (!string.Equals(EmployerName, next.EmployerName, StringComparison.Ordinal))
        {
            EmployerName = next.EmployerName;
            anyChanged = true;
        }

        if (WorkSeniorityMonths != next.WorkSeniorityMonths)
        {
            WorkSeniorityMonths = next.WorkSeniorityMonths;
            anyChanged = true;
        }

        if (anyChanged)
        {
            Touch(clock);
        }

        return criticalChanges;
    }

    public void TouchVersionForUpdate(IClock clock) => Touch(clock);

    public void MarkEmailVerified(IClock clock)
    {
        EmailVerifiedAt = clock.UtcNow;
        Touch(clock);
    }

    public void MarkPhoneVerified(IClock clock)
    {
        PhoneVerifiedAt = clock.UtcNow;
        Touch(clock);
    }

    public void LinkPortalUser(Guid portalUserId, IClock clock)
    {
        PortalUserId = portalUserId;
        Touch(clock);
    }

    public IReadOnlyList<DocumentCategory> GetRequiredDocuments(decimal incomeProofThreshold)
    {
        if (PersonType == PersonType.JURIDICA)
        {
            return [DocumentCategory.FICHA_RUC];
        }

        var required = new List<DocumentCategory>
        {
            DocumentCategory.DNI_FRONTAL,
            DocumentCategory.DNI_REVERSO,
            DocumentCategory.SELFIE
        };

        if (MonthlyNetIncome.Amount > incomeProofThreshold)
        {
            required.Add(DocumentCategory.BOLETA_PAGO);
        }

        return required;
    }

    public Result SubmitKyc(
        IReadOnlySet<DocumentCategory> presentCategories,
        decimal incomeProofThreshold,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(presentCategories);
        ArgumentNullException.ThrowIfNull(clock);

        if (KycStatus != KycStatus.PENDING)
        {
            return CustomerErrors.InvalidKycTransition(
                $"Cannot submit KYC from status '{KycStatus}'. Resubmit first.");
        }

        if (KycSubmittedAt.HasValue)
        {
            return CustomerErrors.InvalidKycTransition("KYC dossier has already been submitted for review.");
        }

        var required = GetRequiredDocuments(incomeProofThreshold);
        var missing = required
            .Where(cat => !presentCategories.Contains(cat))
            .Select(cat => cat.ToString())
            .ToArray();

        if (missing.Length > 0)
        {
            return CustomerErrors.MissingRequiredDocuments(missing);
        }

        KycSubmittedAt = clock.UtcNow;
        Touch(clock);
        return Result.Success();
    }

    public Result ApproveKyc(
        Guid verifiedBy,
        string? notes,
        bool isAdmin,
        IReadOnlyCollection<KycVerification> latestVerifications,
        IClock clock,
        int validityMonths)
    {
        ArgumentNullException.ThrowIfNull(latestVerifications);
        ArgumentNullException.ThrowIfNull(clock);

        if (KycStatus != KycStatus.PENDING || !KycSubmittedAt.HasValue)
        {
            return CustomerErrors.InvalidKycTransition(
                $"Cannot approve KYC from status '{KycStatus}' (submitted={KycSubmittedAt.HasValue}).");
        }

        if (latestVerifications.Any(v => v.Result == KycCheckResult.FAIL))
        {
            return CustomerErrors.KycCheckFailed;
        }

        if ((IsPep || AmlRiskLevel == AmlRiskLevel.HIGH) && !isAdmin)
        {
            return CustomerErrors.KycRequiresAdmin;
        }

        var requiresDetailedNotes = latestVerifications.Any(
            v => v.Result is KycCheckResult.REVIEW or KycCheckResult.ERROR);
        var minLength = requiresDetailedNotes
            ? ReviewOrErrorApprovalNotesMinLength
            : StandardApprovalNotesMinLength;

        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? string.Empty : notes.Trim();
        if (trimmedNotes.Length < minLength)
        {
            return CustomerErrors.KycNotesRequired(minLength);
        }

        if (trimmedNotes.Length > MaxNotesLength)
        {
            return CustomerErrors.Validation($"KYC notes must not exceed {MaxNotesLength} characters.");
        }

        var now = clock.UtcNow;
        KycStatus = KycStatus.VERIFIED;
        KycVerifiedBy = verifiedBy == Guid.Empty ? null : verifiedBy;
        KycVerifiedAt = now;
        KycNotes = trimmedNotes;
        KycRejectedReason = null;
        KycExpiresAt = now.AddMonths(validityMonths);
        Touch(clock);
        return Result.Success();
    }

    public Result RejectKyc(Guid rejectedBy, string? reason, bool isAdmin, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (KycStatus == KycStatus.VERIFIED)
        {
            if (!isAdmin)
            {
                return CustomerErrors.KycRequiresAdmin;
            }
        }
        else if (KycStatus != KycStatus.PENDING || !KycSubmittedAt.HasValue)
        {
            return CustomerErrors.InvalidKycTransition(
                $"Cannot reject KYC from status '{KycStatus}' (submitted={KycSubmittedAt.HasValue}).");
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        if (trimmedReason.Length < StandardApprovalNotesMinLength)
        {
            return CustomerErrors.KycNotesRequired(StandardApprovalNotesMinLength);
        }

        if (trimmedReason.Length > MaxNotesLength)
        {
            return CustomerErrors.Validation($"Rejection reason must not exceed {MaxNotesLength} characters.");
        }

        KycStatus = KycStatus.REJECTED;
        KycVerifiedBy = rejectedBy == Guid.Empty ? null : rejectedBy;
        KycVerifiedAt = clock.UtcNow;
        KycRejectedReason = trimmedReason;
        KycExpiresAt = null;
        Touch(clock);
        return Result.Success();
    }

    public Result ResubmitKyc(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (KycStatus is not (KycStatus.REJECTED or KycStatus.EXPIRED))
        {
            return CustomerErrors.InvalidKycTransition(
                $"Cannot resubmit KYC from status '{KycStatus}'. Only REJECTED or EXPIRED dossiers can be resubmitted.");
        }

        KycStatus = KycStatus.PENDING;
        KycSubmittedAt = null;
        KycVerifiedBy = null;
        KycVerifiedAt = null;
        KycExpiresAt = null;
        KycRejectedReason = null;
        KycNotes = null;
        Touch(clock);
        return Result.Success();
    }

    public Result ExpireKyc(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (KycStatus != KycStatus.VERIFIED || !KycExpiresAt.HasValue || KycExpiresAt.Value > clock.UtcNow)
        {
            return CustomerErrors.InvalidKycTransition("Customer KYC is not eligible for expiration.");
        }

        KycStatus = KycStatus.EXPIRED;
        Touch(clock);
        return Result.Success();
    }

    public bool IsKycEffectivelyExpired(DateTimeOffset utcNow) =>
        KycStatus == KycStatus.EXPIRED ||
        (KycStatus == KycStatus.VERIFIED && KycExpiresAt.HasValue && KycExpiresAt.Value <= utcNow);

    public void MarkAsPep(IClock clock)
    {
        IsPep = true;
        AmlRiskLevel = AmlRiskLevel.HIGH;
        Touch(clock);
    }

    public void MarkHighAmlRisk(IClock clock)
    {
        AmlRiskLevel = AmlRiskLevel.HIGH;
        Touch(clock);
    }

    private void Touch(IClock clock)
    {
        UpdatedAt = clock.UtcNow;
        Version++;
    }

    private static int CalculateAge(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static Result<(string Address, string District)> ValidateAddress(string? rawAddress, string? rawDistrict)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            return CustomerErrors.Validation("Address is required.");
        }

        if (string.IsNullOrWhiteSpace(rawDistrict))
        {
            return CustomerErrors.Validation("District is required.");
        }

        var address = rawAddress.Trim();
        var district = rawDistrict.Trim();

        if (address.Length > MaxAddressLength)
        {
            return CustomerErrors.Validation($"Address must not exceed {MaxAddressLength} characters.");
        }

        if (district.Length > MaxDistrictLength)
        {
            return CustomerErrors.Validation($"District must not exceed {MaxDistrictLength} characters.");
        }

        return (address, district);
    }
}
