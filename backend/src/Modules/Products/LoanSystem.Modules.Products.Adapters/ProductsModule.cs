using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Products.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Products.Core.Ports.Outbound;
using LoanSystem.Modules.Products.Core.Application;
using LoanSystem.Modules.Products.Core.Ports.Inbound;

namespace LoanSystem.Modules.Products.Adapters;

public static class ProductsModule
{
    public static IServiceCollection AddProductsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<ProductsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IProductsUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());
        services.AddScoped<IProductsOutbox>(sp => sp.GetRequiredService<ProductsDbContext>());

        // Use cases
        services.AddScoped<ISimulateScheduleUseCase, SimulateScheduleUseCase>();

        return services;
    }
}
