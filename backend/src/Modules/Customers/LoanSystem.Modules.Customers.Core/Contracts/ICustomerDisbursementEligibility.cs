namespace LoanSystem.Modules.Customers.Core.Contracts;

/// <summary>Reason codes returned when a customer or bank account is not eligible for disbursement.</summary>
public static class DisbursementEligibilityReasons
{
    public const string KycNotVerified = "KYC_NOT_VERIFIED";
    public const string KycExpired = "KYC_EXPIRED";
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountNotOwned = "ACCOUNT_NOT_OWNED";
    public const string AccountNotVerified = "ACCOUNT_NOT_VERIFIED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string AccountInCoolingPeriod = "ACCOUNT_IN_COOLING_PERIOD";
}

public sealed record DisbursementEligibility(bool IsEligible, IReadOnlyList<string> Reasons);

/// <summary>
/// Public contract consumed by Loans/Treasury inside a disbursement transaction
/// to enforce verified KYC and active, verified, cooled-off bank account ownership (BE-CUS-06).
/// </summary>
public interface ICustomerDisbursementEligibility
{
    Task<DisbursementEligibility> CheckAsync(Guid customerId, Guid bankAccountId, CancellationToken ct = default);
}
