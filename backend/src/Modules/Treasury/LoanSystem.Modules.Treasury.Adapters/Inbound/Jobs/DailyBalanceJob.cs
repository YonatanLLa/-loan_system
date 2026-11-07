using LoanSystem.Modules.Treasury.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Treasury.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class DailyBalanceJob : IJob
{
    private readonly IRunDailyBalanceUseCase _useCase;
    private readonly ILogger<DailyBalanceJob> _logger;

    public DailyBalanceJob(IRunDailyBalanceUseCase useCase, ILogger<DailyBalanceJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing DailyBalanceJob at 06:00 (Lima time)...");

        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("DailyBalanceJob failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
        }
    }
}
