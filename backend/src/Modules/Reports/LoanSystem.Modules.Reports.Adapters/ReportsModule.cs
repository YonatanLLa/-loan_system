using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Reports.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Reports.Core.Ports.Outbound;

namespace LoanSystem.Modules.Reports.Adapters;

public static class ReportsModule
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<ReportsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IReportsUnitOfWork>(sp => sp.GetRequiredService<ReportsDbContext>());
        services.AddScoped<IReportsOutbox>(sp => sp.GetRequiredService<ReportsDbContext>());

        return services;
    }
}
