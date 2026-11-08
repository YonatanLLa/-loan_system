using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Notifications.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Notifications.Core.Ports.Outbound;
using LoanSystem.Modules.Notifications.Core.Application;
using LoanSystem.Modules.Notifications.Core.Ports.Inbound;

namespace LoanSystem.Modules.Notifications.Adapters;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddAuditLogging();
        services.AddDbContext<NotificationsDbContext>((sp, options) =>
            options.UseNpgsql(connectionString).UseAuditLog(sp));
        services.AddScoped<INotificationsUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationsOutbox>(sp => sp.GetRequiredService<NotificationsDbContext>());

        // Use cases
        services.AddScoped<ISendDailyRemindersUseCase, SendDailyRemindersUseCase>();

        return services;
    }
}
