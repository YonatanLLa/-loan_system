using LoanSystem.Modules.Collections.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Collections.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class DailyMoraCalculationJob : IJob
{
    private readonly ICalculateDailyMoraUseCase _useCase;
    private readonly ILogger<DailyMoraCalculationJob> _logger;

    public DailyMoraCalculationJob(ICalculateDailyMoraUseCase useCase, ILogger<DailyMoraCalculationJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing DailyMoraCalculationJob at 00:05 (Lima time)...");

        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("DailyMoraCalculationJob failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
        }
    }
}
