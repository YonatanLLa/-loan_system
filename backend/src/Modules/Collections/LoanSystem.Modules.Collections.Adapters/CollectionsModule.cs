using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Collections.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Collections.Core.Ports.Outbound;
using LoanSystem.Modules.Collections.Core.Application;
using LoanSystem.Modules.Collections.Core.Ports.Inbound;

namespace LoanSystem.Modules.Collections.Adapters;

public static class CollectionsModule
{
    public static IServiceCollection AddCollectionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<CollectionsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ICollectionsUnitOfWork>(sp => sp.GetRequiredService<CollectionsDbContext>());
        services.AddScoped<ICollectionsOutbox>(sp => sp.GetRequiredService<CollectionsDbContext>());

        // Use cases
        services.AddScoped<ICalculateDailyMoraUseCase, CalculateDailyMoraUseCase>();

        return services;
    }
}
