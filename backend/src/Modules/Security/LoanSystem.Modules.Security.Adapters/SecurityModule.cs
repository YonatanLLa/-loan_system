using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Security.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters;

public static class SecurityModule
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<SecurityDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ISecurityUnitOfWork>(sp => sp.GetRequiredService<SecurityDbContext>());
        services.AddScoped<ISecurityOutbox>(sp => sp.GetRequiredService<SecurityDbContext>());

        return services;
    }
}
