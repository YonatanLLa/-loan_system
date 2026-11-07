using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.Common.Adapters.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared Dapper connection factory and the outbox dispatcher pipeline.
    /// Requires an <see cref="LoanSystem.Common.Core.Ports.Outbound.IClock"/> registration.
    /// </summary>
    public static IServiceCollection AddCommonPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredConnectionString("Database");

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        GetOrAddRegistry(services);
        services.AddSingleton<OutboxProcessor>();

        return services;
    }

    /// <summary>Registers a handler and makes its event type resolvable by the outbox dispatcher.</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        GetOrAddRegistry(services).Register(typeof(TEvent));
        return services;
    }

    // The registry is built while services are registered, so it is kept as a single instance in the collection
    private static IntegrationEventTypeRegistry GetOrAddRegistry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(IntegrationEventTypeRegistry))?
            .ImplementationInstance as IntegrationEventTypeRegistry;

        if (existing is not null)
            return existing;

        var registry = new IntegrationEventTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
