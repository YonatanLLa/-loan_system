using LoanSystem.Modules.Loans.Core.Contracts;

namespace LoanSystem.Modules.Loans.Adapters.Outbound.Persistence;

// TODO(BE-LOA-xx): Reemplazar cuando el módulo Loans implemente la persistencia de préstamos.
internal sealed class EmptyLoanPortfolioQuery : ILoanPortfolioQuery
{
    public Task<IReadOnlyList<LoanSummary>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LoanSummary>>([]);
}
