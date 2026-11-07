using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Common.Adapters.Outbox;

[DisallowConcurrentExecution]
internal sealed class OutboxDispatcherJob : IJob
{
    private readonly OutboxProcessor _processor;
    private readonly ILogger<OutboxDispatcherJob> _logger;

    public OutboxDispatcherJob(OutboxProcessor processor, ILogger<OutboxDispatcherJob> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var processed = await _processor.ProcessBatchAsync(cancellationToken);
        if (processed > 0)
        {
            _logger.LogInformation("Outbox dispatcher processed {Count} message(s)", processed);
        }
    }
}
