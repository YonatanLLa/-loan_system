using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Products.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Products.Core.Ports.Outbound;
using LoanSystem.Modules.Products.Core.Application;
using LoanSystem.Modules.Products.Core.Ports.Inbound;
using LoanSystem.Modules.Products.Adapters.Outbound.Administration;
using Microsoft.Extensions.Options;

namespace LoanSystem.Modules.Products.Adapters;

public static class ProductsModule
{
    public static IServiceCollection AddProductsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddOptions<ProductsOptions>()
            .Bind(configuration.GetSection(ProductsOptions.SectionName))
            .Validate(o => o.Engine.MinDaysToFirstInstallment is >= 0 and <= 31,
                "Products:Engine:MinDaysToFirstInstallment must be between 0 and 31.")
            .Validate(o => o.Engine.ResolvedNonBusinessWeekdays.Count < 7,
                "Products:Engine:NonBusinessWeekdays must leave at least one business weekday.")
            .Validate(o => o.Cache.HolidaysMinutes > 0, "Products:Cache:HolidaysMinutes must be greater than 0.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ProductsOptions>>().Value);

        services.AddAuditLogging();
        services.AddMemoryCache();
        services.AddDbContext<ProductsDbContext>((sp, options) =>
            options.UseNpgsql(connectionString).UseAuditLog(sp));
        services.AddScoped<IProductsUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());
        services.AddScoped<IProductsOutbox>(sp => sp.GetRequiredService<ProductsDbContext>());

        // Use cases
        services.AddScoped<ISimulateScheduleUseCase, SimulateScheduleUseCase>();

        // Due dates and holidays (BE-PRO-06; holidays come from Administration's public contract)
        services.AddScoped<IHolidayProvider, AdministrationHolidayProvider>();
        services.AddScoped<DueDateService>();

        return services;
    }
}
