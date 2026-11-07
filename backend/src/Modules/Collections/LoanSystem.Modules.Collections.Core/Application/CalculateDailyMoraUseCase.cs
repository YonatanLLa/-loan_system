using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Collections.Core.Ports.Inbound;

namespace LoanSystem.Modules.Collections.Core.Application;

internal sealed class CalculateDailyMoraUseCase : ICalculateDailyMoraUseCase
{
    public Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: compute daily late fee, update overdue days, transit loans to OVERDUE
        return Task.FromResult(Result.Success());
    }
}
