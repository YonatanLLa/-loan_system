using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Administration.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Administration.Core.Ports.Outbound;

namespace LoanSystem.Modules.Administration.Adapters;

public static class AdministrationModule
{
    public static IServiceCollection AddAdministrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<AdministrationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IAdministrationUnitOfWork>(sp => sp.GetRequiredService<AdministrationDbContext>());
        services.AddScoped<IAdministrationOutbox>(sp => sp.GetRequiredService<AdministrationDbContext>());

        return services;
    }
}
