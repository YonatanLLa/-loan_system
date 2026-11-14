using System.Net;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

/// <summary>Database unique-constraint exception translated by outbound persistence adapters without leaking EF/Npgsql types into Core.</summary>
internal sealed class DuplicateRecordException : Exception
{
    public string? ConstraintName { get; }

    public DuplicateRecordException(string? constraintName, Exception inner)
        : base($"Duplicate record violating unique constraint '{constraintName}'.", inner)
    {
        ConstraintName = constraintName;
    }
}

/// <summary>Writes explicit security/compliance audit events when no entity state change occurs (e.g., VIEW_DOCUMENT, OTP_FAILED, OTP_LOCKED).</summary>
internal interface ICustomerAuditWriter
{
    Task WriteAsync(
        string entityName,
        string entityId,
        string action,
        Guid? userId,
        IPAddress? userIp,
        string? userAgent,
        string? metadataJson = null,
        CancellationToken ct = default);
}

/// <summary>Direct lookup of bank accounts across customers to distinguish ACCOUNT_NOT_FOUND from ACCOUNT_NOT_OWNED in BE-CUS-06.</summary>
internal interface IBankAccountExistenceLookup
{
    Task<(bool Exists, Domain.CustomerBankAccount? Account)> FindGlobalByIdAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>Safe diagnostic logging port (zero PII) so Core remains free of logging package dependencies.</summary>
internal interface ICustomerDiagnostics
{
    void LogOrphanedDocumentObject(string storageKey, Guid customerId, Exception exception);
    void LogKycCheckAttemptFailure(Domain.KycCheckType checkType, Guid customerId, int attempt, int maxAttempts, Exception exception);
}
