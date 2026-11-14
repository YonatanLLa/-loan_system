using LoanSystem.Modules.Customers.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal sealed record IdentityRegistryQuery(
    Guid CustomerId,
    PersonType PersonType,
    DocumentType DocType,
    string DocNumber,
    string FullName,
    DateOnly? BirthDate);

internal sealed record BiometricQuery(
    Guid CustomerId,
    string DocNumber,
    string? SelfieStorageUri,
    string? DocumentFrontStorageUri);

internal sealed record WatchlistQuery(
    Guid CustomerId,
    PersonType PersonType,
    DocumentType DocType,
    string DocNumber,
    string FullName);

internal sealed record KycCheckOutcome(
    KycCheckType Type,
    KycCheckResult Result,
    decimal? Score,
    string? ExternalReference,
    string RawResponseJson,
    string? Error,
    bool IsPepFlag = false,
    bool IsSanctionsHit = false,
    bool IsTransientError = false);

internal interface IIdentityVerifier
{
    string ProviderName { get; }
    Task<KycCheckOutcome> VerifyRegistryAsync(IdentityRegistryQuery query, CancellationToken ct = default);
    Task<KycCheckOutcome> VerifyLivenessAsync(BiometricQuery query, CancellationToken ct = default);
    Task<KycCheckOutcome> ScreenWatchlistsAsync(WatchlistQuery query, CancellationToken ct = default);
}

internal interface IKycVerificationRepository
{
    void Add(KycVerification verification);
    Task<IReadOnlyList<KycVerification>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<KycVerification>> GetLatestByCustomerAsync(Guid customerId, CancellationToken ct = default);
}
