using LoanSystem.Modules.Notifications.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Notifications.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class DailyRemindersJob : IJob
{
    private readonly ISendDailyRemindersUseCase _useCase;
    private readonly ILogger<DailyRemindersJob> _logger;

    public DailyRemindersJob(ISendDailyRemindersUseCase useCase, ILogger<DailyRemindersJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing DailyRemindersJob at 09:00 (Lima time)...");

        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("DailyRemindersJob failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
        }
    }
}
