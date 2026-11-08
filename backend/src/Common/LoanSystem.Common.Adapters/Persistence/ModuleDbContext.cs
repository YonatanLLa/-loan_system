using System.Text.Json;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>
/// Base DbContext for every module. Each module exposes it through its own
/// I{Module}UnitOfWork and I{Module}Outbox ports so use cases never depend on EF Core.
/// Outbox rows are tracked by this context, so they commit in the same SaveChanges
/// (and transaction) as the module's business data.
/// </summary>
public abstract class ModuleDbContext : DbContext, IUnitOfWork, IOutbox
{
    internal static readonly JsonSerializerOptions OutboxSerializerOptions = new(JsonSerializerDefaults.Web);

    protected ModuleDbContext(DbContextOptions options) : base(options) { }

    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await Database.BeginTransactionAsync(ct);
        return new EfTransactionScope(transaction);
    }

    public void Enqueue<TEvent>(TEvent @event) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Serialize with the runtime type so derived events keep all their data
        var eventType = @event.GetType();
        var integrationEvent = @event as IIntegrationEvent;

        Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent?.EventId ?? Guid.NewGuid(),
            EventType = eventType.FullName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(@event, eventType, OutboxSerializerOptions),
            // timestamptz only accepts UTC offsets in Npgsql
            OccurredOn = (integrationEvent?.OccurredOn ?? DateTimeOffset.UtcNow).ToUniversalTime()
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogRecordConfiguration());
    }
}
