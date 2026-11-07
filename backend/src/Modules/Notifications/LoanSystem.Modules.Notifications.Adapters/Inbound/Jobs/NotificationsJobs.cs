using Quartz;

namespace LoanSystem.Modules.Notifications.Adapters.Inbound.Jobs;

public static class NotificationsJobs
{
    public static IQuartzBuilder AddNotificationsJobs(this IQuartzBuilder quartz, TimeZoneInfo timeZone)
    {
        var dailyRemindersJobKey = new JobKey("DailyRemindersJob");
        quartz.AddJob<DailyRemindersJob>(opts => opts.WithIdentity(dailyRemindersJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(dailyRemindersJobKey)
            .WithIdentity("DailyRemindersTrigger")
            .WithCronSchedule("0 0 9 * * ?", x => x.InTimeZone(timeZone)));

        return quartz;
    }
}
