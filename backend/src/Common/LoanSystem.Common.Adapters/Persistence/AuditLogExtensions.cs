using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>Shared wiring for the audit trail (BE-SEC-07), used by every module's DbContext.</summary>
public static class AuditLogExtensions
{
    internal const string ExcludedAnnotation = "LoanSystem:Audit:Excluded";
    internal const string SensitiveAnnotation = "LoanSystem:Audit:Sensitive";

    /// <summary>
    /// Registers the audit interceptor and the per-request <see cref="IAuditContext"/>.
    /// Idempotent: every module calls it, the services are registered only once.
    /// </summary>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        services.TryAddScoped<IAuditContext, AuditContext>();
        services.TryAddScoped<AuditLogInterceptor>();
        return services;
    }

    /// <summary>Attaches the audit interceptor to a module DbContext. Call inside AddDbContext((sp, options) => ...).</summary>
    public static DbContextOptionsBuilder UseAuditLog(this DbContextOptionsBuilder options, IServiceProvider serviceProvider) =>
        options.AddInterceptors(serviceProvider.GetRequiredService<AuditLogInterceptor>());

    /// <summary>
    /// Opts an entity out of the audit trail (technical, high-volume rows such as refresh tokens).
    /// Kept in the EF mapping so domain classes stay free of persistence concerns.
    /// </summary>
    public static EntityTypeBuilder<TEntity> ExcludeFromAudit<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.HasAnnotation(ExcludedAnnotation, true);
        return builder;
    }

    internal static bool IsExcludedFromAudit(this IReadOnlyEntityType entityType) =>
        entityType.FindAnnotation(ExcludedAnnotation)?.Value is true;

    /// <summary>
    /// Marks a property whose value must never reach the audit trail: it is logged as [REDACTED].
    /// Use it for secrets whose name the interceptor's naming rules would not catch (e.g. InvitationCode, ApiKey).
    /// </summary>
    public static PropertyBuilder<TProperty> IsAuditSensitive<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        builder.HasAnnotation(SensitiveAnnotation, true);
        return builder;
    }

    internal static bool IsAuditSensitive(this IReadOnlyProperty property) =>
        property.FindAnnotation(SensitiveAnnotation)?.Value is true;
}
