using System.Reflection;
using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Common.Adapters.Outbox;

/// <summary>
/// Publishes pending outbox messages to their in-process handlers. Rows are locked with
/// FOR UPDATE SKIP LOCKED so several workers can run concurrently without double processing.
/// Delivery is at-least-once: if the status update fails after a handler ran, the message is retried.
/// </summary>
public sealed class OutboxProcessor
{
    public const int BatchSize = 50;
    public const int MaxRetries = 5;

    private const string SelectPendingSql = """
        SELECT id AS Id, event_type AS EventType, payload::text AS Payload
        FROM outbox_messages
        WHERE processed_on IS NULL AND retry_count < @MaxRetries
        ORDER BY occurred_on
        LIMIT @BatchSize
        FOR UPDATE SKIP LOCKED
        """;

    private const string MarkProcessedSql =
        "UPDATE outbox_messages SET processed_on = @Now, error = NULL WHERE id = @Id";

    private const string MarkFailedSql =
        "UPDATE outbox_messages SET retry_count = retry_count + 1, error = @Error WHERE id = @Id";

    private const string MarkUnprocessableSql =
        "UPDATE outbox_messages SET retry_count = @MaxRetries, error = @Error WHERE id = @Id";

    private static readonly MethodInfo DispatchMethod =
        typeof(OutboxProcessor).GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IDbConnectionFactory connectionFactory,
        IntegrationEventTypeRegistry registry,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<OutboxProcessor> logger)
    {
        _connectionFactory = connectionFactory;
        _registry = registry;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Processes one batch and returns how many messages were picked up.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var messages = (await connection.QueryAsync<PendingMessage>(new CommandDefinition(
            SelectPendingSql, new { MaxRetries, BatchSize }, transaction, cancellationToken: ct))).ToList();

        foreach (var message in messages)
        {
            if (!_registry.TryResolve(message.EventType, out var eventType))
            {
                // No handler knows this type: park it instead of retrying forever
                _logger.LogError("Outbox message {Id} has unknown event type {EventType}", message.Id, message.EventType);
                await connection.ExecuteAsync(new CommandDefinition(MarkUnprocessableSql,
                    new { message.Id, MaxRetries, Error = $"Unknown event type '{message.EventType}' (no handler registered)." },
                    transaction, cancellationToken: ct));
                continue;
            }

            try
            {
                var integrationEvent = (IIntegrationEvent?)JsonSerializer.Deserialize(
                        message.Payload, eventType, ModuleDbContext.OutboxSerializerOptions)
                    ?? throw new InvalidOperationException("Payload deserialized to null.");

                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    await (Task)DispatchMethod.MakeGenericMethod(eventType)
                        .Invoke(null, [scope.ServiceProvider, integrationEvent, ct])!;
                }

                await connection.ExecuteAsync(new CommandDefinition(MarkProcessedSql,
                    new { message.Id, Now = _clock.UtcNow }, transaction, cancellationToken: ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Outbox message {Id} ({EventType}) failed", message.Id, message.EventType);
                await connection.ExecuteAsync(new CommandDefinition(MarkFailedSql,
                    new { message.Id, Error = ex.ToString() }, transaction, cancellationToken: ct));
            }
        }

        transaction.Commit();
        return messages.Count;
    }

    private static async Task DispatchAsync<TEvent>(IServiceProvider services, TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, ct);
        }
    }

    private sealed class PendingMessage
    {
        public Guid Id { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
    }
}
