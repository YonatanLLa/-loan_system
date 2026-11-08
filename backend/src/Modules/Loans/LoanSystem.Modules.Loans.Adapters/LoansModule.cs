using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Loans.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Loans.Core.Ports.Outbound;

namespace LoanSystem.Modules.Loans.Adapters;

public static class LoansModule
{
    public static IServiceCollection AddLoansModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddAuditLogging();
        services.AddDbContext<LoansDbContext>((sp, options) =>
            options.UseNpgsql(connectionString).UseAuditLog(sp));
        services.AddScoped<ILoansUnitOfWork>(sp => sp.GetRequiredService<LoansDbContext>());
        services.AddScoped<ILoansOutbox>(sp => sp.GetRequiredService<LoansDbContext>());

        return services;
    }
}
