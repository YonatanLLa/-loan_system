using Quartz;

namespace LoanSystem.Common.Adapters.Outbox;

public static class OutboxJobs
{
    public static IQuartzBuilder AddOutboxJobs(this IQuartzBuilder quartz)
    {
        var outboxJobKey = new JobKey("OutboxDispatcherJob");
        quartz.AddJob<OutboxDispatcherJob>(opts => opts.WithIdentity(outboxJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(outboxJobKey)
            .WithIdentity("OutboxTrigger")
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(10)).RepeatForever()));

        return quartz;
    }
}
