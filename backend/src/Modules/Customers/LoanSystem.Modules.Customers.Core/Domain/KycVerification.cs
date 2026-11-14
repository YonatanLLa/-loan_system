using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed class KycVerification : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public KycCheckType CheckType { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public KycCheckResult Result { get; private set; }
    public decimal? Score { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? RawResponseJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? RequestedBy { get; private set; }

    private KycVerification() { }

    public static KycVerification CreatePending(
        Guid customerId,
        KycCheckType checkType,
        string provider,
        string? externalReference,
        Guid? requestedBy,
        IClock clock) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CheckType = checkType,
            Provider = provider,
            Result = KycCheckResult.PENDING,
            ExternalReference = externalReference,
            RequestedAt = clock.UtcNow,
            RequestedBy = requestedBy == Guid.Empty ? null : requestedBy
        };

    public void RecordOutcome(
        KycCheckResult result,
        decimal? score,
        string? externalReference,
        string? rawResponseJson,
        string? errorMessage,
        IClock clock)
    {
        Result = result;
        Score = score;
        if (!string.IsNullOrWhiteSpace(externalReference))
        {
            ExternalReference = externalReference.Length > 100 ? externalReference[..100] : externalReference;
        }

        RawResponseJson = rawResponseJson;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? null
            : errorMessage.Length > 500 ? errorMessage[..500] : errorMessage;
        CompletedAt = clock.UtcNow;
    }

    public void ResetForRetry(string provider, Guid? requestedBy, IClock clock)
    {
        Provider = provider;
        Result = KycCheckResult.PENDING;
        Score = null;
        RawResponseJson = null;
        ErrorMessage = null;
        RequestedAt = clock.UtcNow;
        CompletedAt = null;
        RequestedBy = requestedBy == Guid.Empty ? null : requestedBy;
    }
}
