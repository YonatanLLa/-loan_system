using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Treasury.Core.Ports.Inbound;

namespace LoanSystem.Modules.Treasury.Core.Application;

internal sealed class CheckDisbursementsInTransitUseCase : ICheckDisbursementsInTransitUseCase
{
    public Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: query bank gateways for pending disbursements
        return Task.FromResult(Result.Success());
    }
}
