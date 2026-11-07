using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Origination.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Origination.Core.Ports.Outbound;

namespace LoanSystem.Modules.Origination.Adapters;

public static class OriginationModule
{
    public static IServiceCollection AddOriginationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<OriginationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IOriginationUnitOfWork>(sp => sp.GetRequiredService<OriginationDbContext>());
        services.AddScoped<IOriginationOutbox>(sp => sp.GetRequiredService<OriginationDbContext>());

        return services;
    }
}
