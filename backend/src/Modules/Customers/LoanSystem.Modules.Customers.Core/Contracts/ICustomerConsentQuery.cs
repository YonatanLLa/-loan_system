namespace LoanSystem.Modules.Customers.Core.Contracts;

/// <summary>
/// Public contract consumed by Origination (and other modules) to verify active legal consent
/// for a specific purpose such as CREDIT_BUREAU_QUERY (BE-CUS-07).
/// </summary>
public interface ICustomerConsentQuery
{
    Task<bool> HasActiveAsync(Guid customerId, string purpose, CancellationToken ct = default);
}

/// <summary>
/// Public read-only lookup resolved by Security when issuing JWTs for ROLE_CLIENT portal users (R7).
/// </summary>
public interface ICustomerPortalLookup
{
    Task<Guid?> FindCustomerIdByPortalUserIdAsync(Guid portalUserId, CancellationToken ct = default);
}
