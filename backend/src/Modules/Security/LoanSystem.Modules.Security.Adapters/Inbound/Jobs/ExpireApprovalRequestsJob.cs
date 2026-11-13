using LoanSystem.Modules.Security.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Security.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class ExpireApprovalRequestsJob : IJob
{
    private readonly IExpireApprovalRequestsUseCase _useCase;
    private readonly ILogger<ExpireApprovalRequestsJob> _logger;

    public ExpireApprovalRequestsJob(
        IExpireApprovalRequestsUseCase useCase,
        ILogger<ExpireApprovalRequestsJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "ExpireApprovalRequestsJob failed: {Code} - {Description}",
                result.Error.Code,
                result.Error.Description);
            return;
        }

        if (result.Value > 0)
        {
            _logger.LogInformation("Expired {Count} pending approval request(s).", result.Value);
        }
    }
}
