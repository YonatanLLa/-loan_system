using Quartz;

namespace LoanSystem.Modules.Security.Adapters.Inbound.Jobs;

public static class SecurityJobs
{
    public static IQuartzBuilder AddSecurityJobs(this IQuartzBuilder quartz, TimeZoneInfo timeZone)
    {
        var expireApprovalsJobKey = new JobKey("ExpireApprovalRequestsJob");
        quartz.AddJob<ExpireApprovalRequestsJob>(opts => opts.WithIdentity(expireApprovalsJobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(expireApprovalsJobKey)
            .WithIdentity("ExpireApprovalRequestsTrigger")
            .WithCronSchedule("0 0 * * * ?", x => x.InTimeZone(timeZone)));

        return quartz;
    }
}
