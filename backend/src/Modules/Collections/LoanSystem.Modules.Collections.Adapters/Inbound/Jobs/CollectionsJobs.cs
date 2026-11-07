using Quartz;

namespace LoanSystem.Modules.Collections.Adapters.Inbound.Jobs;

public static class CollectionsJobs
{
    public static IQuartzBuilder AddCollectionsJobs(this IQuartzBuilder quartz, TimeZoneInfo timeZone)
    {
        var dailyMoraCalculationJobKey = new JobKey("DailyMoraJob");
        quartz.AddJob<DailyMoraCalculationJob>(opts => opts.WithIdentity(dailyMoraCalculationJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(dailyMoraCalculationJobKey)
            .WithIdentity("DailyMoraTrigger")
            .WithCronSchedule("0 5 0 * * ?", x => x.InTimeZone(timeZone)));

        return quartz;
    }
}
