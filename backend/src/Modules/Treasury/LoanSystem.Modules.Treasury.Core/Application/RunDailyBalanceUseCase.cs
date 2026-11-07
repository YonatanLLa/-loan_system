using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Treasury.Core.Ports.Inbound;

namespace LoanSystem.Modules.Treasury.Core.Application;

internal sealed class RunDailyBalanceUseCase : IRunDailyBalanceUseCase
{
    public Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: bank balance matching against ledger
        return Task.FromResult(Result.Success());
    }
}
