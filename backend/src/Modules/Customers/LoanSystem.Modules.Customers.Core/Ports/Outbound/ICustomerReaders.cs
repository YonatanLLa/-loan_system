using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts;

namespace LoanSystem.Modules.Customers.Core.Ports.Outbound;

internal sealed record CustomerPortfolio(
    IReadOnlyList<CustomerLoanSummary> Loans,
    IReadOnlyList<CustomerApplicationSummary> Applications);

internal interface ICustomerHistoryReader
{
    Task<PagedResult<CustomerChangeEntry>> GetHistoryAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

internal interface ICustomerPortfolioReader
{
    Task<CustomerPortfolio> GetAsync(Guid customerId, CancellationToken ct = default);
}

internal interface IKycExpirationBatchStore
{
    /// <summary>Atomically transitions up to <paramref name="batchSize"/> VERIFIED customers with expired KYC to EXPIRED.</summary>
    Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> ExpireDueBatchAsync(
        DateTimeOffset utcNow,
        int batchSize,
        CancellationToken ct = default);

    /// <summary>Finds VERIFIED customers expiring within <paramref name="windowEndUtc"/> that have not yet been notified recently.</summary>
    Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> FindExpiringSoonAsync(
        DateTimeOffset utcNow,
        DateTimeOffset windowEndUtc,
        int batchSize,
        CancellationToken ct = default);
}
