using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Adapters;

public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<CustomersDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ICustomersUnitOfWork>(sp => sp.GetRequiredService<CustomersDbContext>());
        services.AddScoped<ICustomersOutbox>(sp => sp.GetRequiredService<CustomersDbContext>());

        return services;
    }
}
