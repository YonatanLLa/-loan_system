using System.Net;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Domain.Validators;

namespace LoanSystem.Modules.Customers.Core.Application;

internal static class CustomerMapper
{
    public static bool CanViewUnmaskedPii(ICurrentUser currentUser) =>
        currentUser.IsInRole(SecurityPolicies.Admin) ||
        currentUser.IsInRole(SecurityPolicies.Analyst) ||
        currentUser.IsInRole(SecurityPolicies.Operator);

    public static bool HasCustomerAccess(ICurrentUser currentUser, Guid customerId, params string[] staffRoles)
    {
        foreach (var role in staffRoles)
        {
            if (currentUser.IsInRole(role))
            {
                return true;
            }
        }

        return currentUser.IsInRole(SecurityPolicies.Client) && currentUser.CustomerId == customerId;
    }

    public static CustomerResponse ToResponse(Customer c, ICurrentUser currentUser)
    {
        var unmasked = CanViewUnmaskedPii(currentUser);

        return new CustomerResponse(
            c.Id,
            c.PersonType.ToString(),
            c.Document.Type.ToString(),
            unmasked ? c.Document.Number : PiiMasker.MaskDocNumber(c.Document.Number),
            c.FullName,
            c.BirthDate,
            unmasked ? c.Email : PiiMasker.MaskEmail(c.Email),
            c.EmailVerifiedAt.HasValue,
            unmasked ? c.PhonePrimary : PiiMasker.MaskPhone(c.PhonePrimary),
            c.PhoneVerifiedAt.HasValue,
            unmasked ? c.PhoneSecondary : PiiMasker.MaskPhone(c.PhoneSecondary),
            c.Address,
            c.District,
            c.MonthlyNetIncome.Amount,
            c.Occupation,
            c.EmployerName,
            c.WorkSeniorityMonths,
            c.KycStatus.ToString(),
            c.KycExpiresAt,
            c.IsPep,
            c.AmlRiskLevel.ToString(),
            c.CreditBalance,
            c.Version,
            c.CreatedAt);
    }

    public static CustomerListItem ToListItem(Customer c, ICurrentUser currentUser)
    {
        var unmasked = CanViewUnmaskedPii(currentUser);

        return new CustomerListItem(
            c.Id,
            c.PersonType.ToString(),
            c.Document.Type.ToString(),
            unmasked ? c.Document.Number : PiiMasker.MaskDocNumber(c.Document.Number),
            c.FullName,
            c.KycStatus.ToString(),
            c.AmlRiskLevel.ToString(),
            c.IsPep,
            c.CreatedAt);
    }

    public static CustomerDocumentResponse ToDocumentResponse(CustomerDocument d) =>
        new(
            d.Id,
            d.CustomerId,
            d.DocCategory.ToString(),
            d.FileHash,
            d.MimeType,
            d.SizeBytes,
            d.OriginalName,
            d.IsCurrent,
            d.UploadedAt,
            d.ReplacedAt);

    public static KycVerificationResponse ToVerificationResponse(KycVerification v) =>
        new(
            v.Id,
            v.CheckType.ToString(),
            v.Provider,
            v.Result.ToString(),
            v.Score,
            v.ExternalReference,
            v.ErrorMessage,
            v.RequestedAt,
            v.CompletedAt);

    public static CustomerKycStatusResponse ToKycStatusResponse(
        Customer c,
        IReadOnlyList<CustomerDocument> currentDocs,
        IReadOnlyList<KycVerification> latestVerifications,
        decimal incomeProofThreshold)
    {
        var required = c.GetRequiredDocuments(incomeProofThreshold);
        var presentSet = currentDocs.Select(d => d.DocCategory).ToHashSet();
        var missing = required
            .Where(r => !presentSet.Contains(r))
            .Select(r => r.ToString())
            .ToArray();

        return new CustomerKycStatusResponse(
            c.Id,
            c.KycStatus.ToString(),
            c.KycStatus == KycStatus.PENDING && c.KycSubmittedAt.HasValue,
            c.KycSubmittedAt,
            c.KycVerifiedAt,
            c.KycExpiresAt,
            c.KycRejectedReason,
            c.KycNotes,
            c.IsPep,
            c.AmlRiskLevel.ToString(),
            required.Select(r => r.ToString()).ToArray(),
            missing,
            currentDocs.Select(ToDocumentResponse).ToArray(),
            latestVerifications.Select(ToVerificationResponse).ToArray());
    }

    public static BankAccountResponse ToBankAccountResponse(CustomerBankAccount a) =>
        new(
            a.Id,
            a.BankName,
            a.BankCode,
            a.AccountType.ToString(),
            a.Last4,
            PiiMasker.MaskPhone(a.PhoneWallet),
            a.IsVerified,
            a.IsPrimary,
            a.Status.ToString(),
            a.DisbursementEnabledFrom);
}
