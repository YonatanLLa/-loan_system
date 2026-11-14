using System.Security.Claims;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace LoanSystem.Modules.Security.Adapters;

/// <summary>
/// Registers RS256 JWT bearer authentication, strict key-id validation via <see cref="IJwtKeyRing"/>,
/// startup key-ring verification, and the key-ring health check for HTTP hosts (BE-SEC-10).
/// </summary>
public static class SecurityAuthenticationExtensions
{
    private const int DefaultClockSkewSeconds = 60;

    public static IServiceCollection AddSecurityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.TryAddSingleton(environment);
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<JwtKeyRing>();
        services.TryAddSingleton<IJwtKeyRing>(sp => sp.GetRequiredService<JwtKeyRing>());
        services.TryAddSingleton<IPublicSigningKeysProvider>(sp => sp.GetRequiredService<JwtKeyRing>());
        services.TryAddScoped<IGetJwksUseCase, GetJwksUseCase>();

        var clockSkewSeconds = ParseClockSkewSeconds(configuration);
        var validIssuer = configuration["Jwt:Issuer"] ?? "LoanSystem.Identity";
        var validAudience = configuration["Jwt:Audience"] ?? "LoanSystem.App";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtKeyRing>((options, keyRing) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidIssuer = validIssuer,
                    ValidAudience = validAudience,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    TryAllIssuerSigningKeys = false,
                    ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds),
                    RoleClaimType = ClaimTypes.Role,
                    IssuerSigningKeyResolver = (_, _, kid, _) => keyRing.ResolveValidationKeys(kid)
                };
            });

        services.AddHostedService<JwtKeyRingStartupHostedService>();

        services.AddHealthChecks()
            .AddCheck<JwtKeyRingHealthCheck>("jwt_keyring", tags: ["ready", "security"]);

        return services;
    }

    private static int ParseClockSkewSeconds(IConfiguration configuration)
    {
        var raw = configuration["Jwt:ClockSkewSeconds"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultClockSkewSeconds;
        }

        if (!int.TryParse(raw, out var seconds) || seconds < 0)
        {
            throw new InvalidOperationException(
                "Configuration value 'Jwt:ClockSkewSeconds' must be a non-negative integer.");
        }

        return seconds;
    }

    private sealed class JwtKeyRingStartupHostedService : IHostedService
    {
        private readonly IJwtKeyRing _keyRing;

        public JwtKeyRingStartupHostedService(IJwtKeyRing keyRing)
        {
            _keyRing = keyRing;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _keyRing.EnsureInitialized();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
