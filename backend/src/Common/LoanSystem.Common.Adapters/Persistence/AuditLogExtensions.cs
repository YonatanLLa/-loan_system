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
}
