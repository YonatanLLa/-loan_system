using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Jobs;

[DisallowConcurrentExecution]
internal sealed class ExpireKycJob : IJob
{
    private readonly IExpireKycUseCase _useCase;
    private readonly ILogger<ExpireKycJob> _logger;

    public ExpireKycJob(IExpireKycUseCase useCase, ILogger<ExpireKycJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing ExpireKycJob at 02:00 (Lima time)...");
        var result = await _useCase.ExecuteAsync(cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("ExpireKycJob failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
            return;
        }

        _logger.LogInformation("ExpireKycJob completed. ExpiredCustomers={Count}", result.Value);
    }
}

public static class CustomersJobs
{
    public static IQuartzBuilder AddCustomersJobs(this IQuartzBuilder quartz, TimeZoneInfo timeZone)
    {
        var jobKey = new JobKey("ExpireKycJob");
        quartz.AddJob<ExpireKycJob>(opts => opts.WithIdentity(jobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("ExpireKycTrigger")
            .WithCronSchedule("0 0 2 * * ?", x => x.InTimeZone(timeZone)));

        return quartz;
    }
}
