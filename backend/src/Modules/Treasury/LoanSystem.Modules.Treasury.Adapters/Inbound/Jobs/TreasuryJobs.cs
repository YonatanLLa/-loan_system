using Quartz;

namespace LoanSystem.Modules.Treasury.Adapters.Inbound.Jobs;

public static class TreasuryJobs
{
    public static IQuartzBuilder AddTreasuryJobs(this IQuartzBuilder quartz, TimeZoneInfo timeZone)
    {
        var dailyBalanceJobKey = new JobKey("DailyBalanceJob");
        quartz.AddJob<DailyBalanceJob>(opts => opts.WithIdentity(dailyBalanceJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(dailyBalanceJobKey)
            .WithIdentity("DailyBalanceTrigger")
            .WithCronSchedule("0 0 6 * * ?", x => x.InTimeZone(timeZone)));

        var disbursementsInTransitJobKey = new JobKey("DisbursementsInTransitJob");
        quartz.AddJob<DisbursementsInTransitJob>(opts => opts.WithIdentity(disbursementsInTransitJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(disbursementsInTransitJobKey)
            .WithIdentity("DisbursementsTrigger")
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(15)).RepeatForever()));

        return quartz;
    }
}
