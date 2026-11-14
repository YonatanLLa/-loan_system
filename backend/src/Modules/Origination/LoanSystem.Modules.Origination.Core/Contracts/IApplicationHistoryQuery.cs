namespace LoanSystem.Modules.Origination.Core.Contracts;

public sealed record ApplicationSummary(
    Guid ApplicationId,
    string ApplicationNumber,
    string ProductName,
    decimal RequestedAmount,
    string Currency,
    int TermMonths,
    string Status,
    DateTimeOffset SubmittedAt);

/// <summary>
/// Read-only query contract exposed by Origination for cross-module readers such as Customers Overview (BE-CUS-08).
/// </summary>
public interface IApplicationHistoryQuery
{
    Task<IReadOnlyList<ApplicationSummary>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
}
