using LoanSystem.Modules.Origination.Core.Contracts;

namespace LoanSystem.Modules.Origination.Adapters.Outbound.Persistence;

// TODO(BE-ORI-xx): Reemplazar cuando el módulo Origination implemente la persistencia de solicitudes.
internal sealed class EmptyApplicationHistoryQuery : IApplicationHistoryQuery
{
    public Task<IReadOnlyList<ApplicationSummary>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ApplicationSummary>>([]);
}
