using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Integrations.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Integrations.Core.Ports.Outbound;

namespace LoanSystem.Modules.Integrations.Adapters;

public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddAuditLogging();
        services.AddDbContext<IntegrationsDbContext>((sp, options) =>
            options.UseNpgsql(connectionString).UseAuditLog(sp));
        services.AddScoped<IIntegrationsUnitOfWork>(sp => sp.GetRequiredService<IntegrationsDbContext>());
        services.AddScoped<IIntegrationsOutbox>(sp => sp.GetRequiredService<IntegrationsDbContext>());

        return services;
    }
}
