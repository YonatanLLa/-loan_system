using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Treasury.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Treasury.Core.Ports.Outbound;
using LoanSystem.Modules.Treasury.Core.Application;
using LoanSystem.Modules.Treasury.Core.Ports.Inbound;

namespace LoanSystem.Modules.Treasury.Adapters;

public static class TreasuryModule
{
    public static IServiceCollection AddTreasuryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddAuditLogging();
        services.AddDbContext<TreasuryDbContext>((sp, options) =>
            options.UseNpgsql(connectionString).UseAuditLog(sp));
        services.AddScoped<ITreasuryUnitOfWork>(sp => sp.GetRequiredService<TreasuryDbContext>());
        services.AddScoped<ITreasuryOutbox>(sp => sp.GetRequiredService<TreasuryDbContext>());

        // Use cases
        services.AddScoped<IRunDailyBalanceUseCase, RunDailyBalanceUseCase>();
        services.AddScoped<ICheckDisbursementsInTransitUseCase, CheckDisbursementsInTransitUseCase>();

        return services;
    }
}
