using LoanSystem.Common.Adapters.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Payments.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Payments.Core.Ports.Outbound;

namespace LoanSystem.Modules.Payments.Adapters;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IPaymentsUnitOfWork>(sp => sp.GetRequiredService<PaymentsDbContext>());
        services.AddScoped<IPaymentsOutbox>(sp => sp.GetRequiredService<PaymentsDbContext>());

        return services;
    }
}
