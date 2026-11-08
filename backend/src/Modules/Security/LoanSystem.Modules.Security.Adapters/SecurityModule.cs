using System.Security.Claims;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

        services.AddAuditLogging();
        services.AddDbContext<SecurityDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MapEnum<SecurityRole>("user_role", "public", SecurityDbContext.EnumNameTranslator);
                npgsql.MapEnum<UserStatus>("user_status_type", "public", SecurityDbContext.EnumNameTranslator);
            });
            options.UseAuditLog(sp);
        });
        services.AddScoped<ISecurityUnitOfWork>(sp => sp.GetRequiredService<SecurityDbContext>());
        services.AddScoped<ISecurityOutbox>(sp => sp.GetRequiredService<SecurityDbContext>());

        // Outbound adapters
        services.AddMemoryCache();
        services.AddSingleton<UserStatusService>();
        services.AddSingleton<IUserStatusService>(sp => sp.GetRequiredService<UserStatusService>());
        services.AddSingleton<IUserStatusTracker>(sp => sp.GetRequiredService<UserStatusService>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IClientInfoProvider, HttpClientInfoProvider>();

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var existingOnTokenValidated = options.Events.OnTokenValidated;

            options.Events.OnTokenValidated = async context =>
            {
                if (existingOnTokenValidated is not null)
                {
                    await existingOnTokenValidated(context);
                    if (context.Result?.Failure is not null)
                    {
                        return;
                    }
                }

                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst("sub")?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    var statusService = context.HttpContext.RequestServices.GetRequiredService<IUserStatusService>();
                    var isActive = await statusService.IsUserActiveAsync(userId, context.HttpContext.RequestAborted);
                    if (!isActive)
                    {
                        context.Fail("The user account is suspended or inactive.");
                    }
                }
            };
        });

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
            sp.GetRequiredService<IAuditContext>(),
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
            sp.GetRequiredService<IAuditContext>(),
            refreshTokenDays));

        // Use cases (BE-SEC-04, BE-SEC-06, BE-SEC-08)
        services.AddScoped<ILogoutUseCase, LogoutUseCase>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();
        services.AddScoped<ISuspendUserUseCase, SuspendUserUseCase>();
        services.AddScoped<IActivateUserUseCase, ActivateUserUseCase>();
        services.AddScoped<IQueryAuditLogsUseCase, QueryAuditLogsUseCase>();

        return services;
    }
}
