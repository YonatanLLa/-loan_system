using LoanSystem.Modules.Treasury.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Treasury.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class DisbursementsInTransitJob : IJob
{
    private readonly ICheckDisbursementsInTransitUseCase _useCase;
    private readonly ILogger<DisbursementsInTransitJob> _logger;

    public DisbursementsInTransitJob(ICheckDisbursementsInTransitUseCase useCase, ILogger<DisbursementsInTransitJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking disbursements in transit (every 15 min)...");

        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("DisbursementsInTransitJob failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
        }
    }
}
