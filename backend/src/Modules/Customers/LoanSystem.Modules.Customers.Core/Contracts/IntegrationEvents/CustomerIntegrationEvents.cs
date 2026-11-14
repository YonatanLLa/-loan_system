using LoanSystem.Common.Core.Contracts;

namespace LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents;

public sealed record CustomerRegisteredIntegrationEvent(
    Guid CustomerId,
    string PersonType) : IntegrationEvent;

public sealed record CustomerKycSubmittedIntegrationEvent(
    Guid CustomerId) : IntegrationEvent;

public sealed record CustomerKycVerifiedIntegrationEvent(
    Guid CustomerId,
    DateTimeOffset ExpiresAt) : IntegrationEvent;

public sealed record CustomerKycRejectedIntegrationEvent(
    Guid CustomerId,
    string Reason) : IntegrationEvent;

public sealed record CustomerKycExpiredIntegrationEvent(
    Guid CustomerId,
    DateTimeOffset ExpiresAt) : IntegrationEvent;

public sealed record CustomerKycExpiringSoonIntegrationEvent(
    Guid CustomerId,
    DateTimeOffset ExpiresAt) : IntegrationEvent;

public sealed record CustomerDisbursementAccountChangedIntegrationEvent(
    Guid CustomerId,
    string OldLast4,
    string NewLast4,
    DateTimeOffset EnabledFrom) : IntegrationEvent;

public sealed record CustomerMarketingConsentRevokedIntegrationEvent(
    Guid CustomerId) : IntegrationEvent;

/// <summary>
/// Sensitive integration event carrying the one-time plaintext code solely to Notifications.
/// Never log its payload.
/// </summary>
public sealed record OtpDeliveryRequestedIntegrationEvent(
    Guid CustomerId,
    string Channel,
    string Target,
    string Code,
    string Purpose) : IntegrationEvent;
