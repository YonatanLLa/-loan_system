using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Security.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters;

public static class SecurityModule
{
    private const int DefaultRefreshTokenDays = 14;
    private const int DefaultMaxFailedAttempts = 5;
    private const int DefaultLockWindowMinutes = 15;

    public static IServiceCollection AddSecurityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddDbContext<SecurityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MapEnum<SecurityRole>("user_role", "public", SecurityDbContext.EnumNameTranslator);
                npgsql.MapEnum<UserStatus>("user_status_type", "public", SecurityDbContext.EnumNameTranslator);
            }));
        services.AddScoped<ISecurityUnitOfWork>(sp => sp.GetRequiredService<SecurityDbContext>());
        services.AddScoped<ISecurityOutbox>(sp => sp.GetRequiredService<SecurityDbContext>());

        // Outbound adapters
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IClientInfoProvider, HttpClientInfoProvider>();

        // Use cases (BE-SEC-01, BE-SEC-02)
        var refreshTokenDays = configuration.GetValue("Security:RefreshTokenDays", DefaultRefreshTokenDays);
        var maxFailedAttempts = configuration.GetValue("Security:MaxFailedAttempts", DefaultMaxFailedAttempts);
        var lockWindowMinutes = configuration.GetValue("Security:LockWindowMinutes", DefaultLockWindowMinutes);

        services.AddScoped<ILoginUseCase>(sp => new LoginUseCase(
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IRefreshTokenRepository>(),
            sp.GetRequiredService<IPasswordHasher>(),
            sp.GetRequiredService<IJwtTokenGenerator>(),
            sp.GetRequiredService<IClientInfoProvider>(),
            sp.GetRequiredService<ISecurityUnitOfWork>(),
            sp.GetRequiredService<IClock>(),
            refreshTokenDays,
            maxFailedAttempts,
            lockWindowMinutes));

        services.AddScoped<IRefreshTokenUseCase>(sp => new RefreshTokenUseCase(
            sp.GetRequiredService<IRefreshTokenRepository>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IJwtTokenGenerator>(),
            sp.GetRequiredService<IClientInfoProvider>(),
            sp.GetRequiredService<ISecurityUnitOfWork>(),
            sp.GetRequiredService<IClock>(),
            refreshTokenDays));

        return services;
    }
}
