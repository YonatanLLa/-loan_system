using System.Security.Claims;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LoanSystem.Modules.Security.Adapters.Outbound.Persistence;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Application;
using LoanSystem.Modules.Security.Core.Application.Approvals;
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
        services.AddScoped<IMfaChallengeRepository, MfaChallengeRepository>();
        services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();
        services.AddScoped<ISecurityApprovalRequests, SecurityApprovalRepository>();
        services.AddScoped<IApprovalRequestReader, ApprovalRequestReader>();
        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<JwtKeyRing>();
        services.AddSingleton<IJwtKeyRing>(sp => sp.GetRequiredService<JwtKeyRing>());
        services.AddSingleton<IPublicSigningKeysProvider>(sp => sp.GetRequiredService<JwtKeyRing>());
        services.AddScoped<IGetJwksUseCase, GetJwksUseCase>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
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
                    var roleClaim = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                    var statusService = context.HttpContext.RequestServices.GetRequiredService<IUserStatusService>();
                    var isValid = await statusService.IsUserSessionValidAsync(
                        userId, roleClaim, context.HttpContext.RequestAborted);
                    if (!isValid)
                    {
                        context.Fail("The user account is inactive or its role has changed.");
                    }
                }
            };
        });

        // Use cases (BE-SEC-01, BE-SEC-02, BE-SEC-03)
        var refreshTokenDays = configuration.GetValue("Security:RefreshTokenDays", DefaultRefreshTokenDays);
        var maxFailedAttempts = configuration.GetValue("Security:MaxFailedAttempts", DefaultMaxFailedAttempts);
        var lockWindowMinutes = configuration.GetValue("Security:LockWindowMinutes", DefaultLockWindowMinutes);

        var mfaSection = configuration.GetSection("Security:Mfa");
        var requiredRoleNames = mfaSection.GetSection("RequiredRoles").Get<string[]>();
        var requiredRoles = requiredRoleNames is { Length: > 0 }
            ? requiredRoleNames
                .Select(name => Enum.Parse<SecurityRole>(name, ignoreCase: true))
                .ToHashSet()
            : new HashSet<SecurityRole> { SecurityRole.ROLE_ADMIN, SecurityRole.ROLE_OPERATOR };

        var mfaOptions = new MfaOptions
        {
            RequiredRoles = requiredRoles,
            Issuer = mfaSection.GetValue("Issuer", "LoanSystem")!,
            ChallengeMinutes = mfaSection.GetValue("ChallengeMinutes", 5),
            EnrollmentChallengeMinutes = mfaSection.GetValue("EnrollmentChallengeMinutes", 10),
            MaxAttemptsPerChallenge = mfaSection.GetValue("MaxAttemptsPerChallenge", 5),
            AllowedDriftSteps = mfaSection.GetValue("AllowedDriftSteps", 1)
        };
        var securityOptions = new SecurityOptions
        {
            RefreshTokenDays = refreshTokenDays,
            MaxFailedAttempts = maxFailedAttempts,
            LockWindowMinutes = lockWindowMinutes,
            Mfa = mfaOptions
        };

        services.AddSingleton(securityOptions);
        services.AddSingleton<ITotpService>(_ => new OtpNetTotpService(mfaOptions.Issuer, mfaOptions.AllowedDriftSteps));

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
            lockWindowMinutes,
            sp.GetRequiredService<IMfaChallengeRepository>(),
            mfaOptions));

        services.AddScoped<IVerifyMfaUseCase, VerifyMfaUseCase>();
        services.AddScoped<IBeginMfaEnrollmentUseCase, BeginMfaEnrollmentUseCase>();
        services.AddScoped<IConfirmMfaEnrollmentUseCase, ConfirmMfaEnrollmentUseCase>();
        services.AddScoped<IResetUserMfaUseCase, ResetUserMfaUseCase>();

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

        // Four-eyes approval engine & user role change (BE-SEC-09)
        var approvalsSection = configuration.GetSection("Approvals");
        var expirationHours = approvalsSection.GetSection("ExpirationHours").Get<Dictionary<string, int>>()
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mfaStepUpOps = approvalsSection.GetSection("MfaStepUpOperations").Get<string[]>();
        var approvalsOptions = new ApprovalsOptions
        {
            DefaultExpirationHours = approvalsSection.GetValue("DefaultExpirationHours", 72),
            ExpirationHours = new Dictionary<string, int>(expirationHours, StringComparer.OrdinalIgnoreCase),
            MfaStepUpOperations = mfaStepUpOps is { Length: > 0 } ? mfaStepUpOps : ["*"],
            WriteOffMinDaysLate = approvalsSection.GetValue("WriteOffMinDaysLate", 180)
        };
        services.TryAddSingleton(approvalsOptions);

        services.AddScoped<IQueryApprovalsUseCase, QueryApprovalsUseCase>();
        services.AddScoped<IGetApprovalUseCase, GetApprovalUseCase>();
        services.AddScoped<IApproveRequestUseCase, ApproveRequestUseCase>();
        services.AddScoped<IRejectRequestUseCase, RejectRequestUseCase>();
        services.AddScoped<ICancelRequestUseCase, CancelRequestUseCase>();
        services.AddScoped<IExpireApprovalRequestsUseCase, ExpireApprovalRequestsUseCase>();
        services.AddScoped<IRequestUserRoleChangeUseCase, RequestUserRoleChangeUseCase>();
        services.AddScoped<IApprovalHandler, UserRoleChangeApprovalHandler>();

        return services;
    }
}
