using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanSystem.Common.Adapters.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedOn { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>Maps <see cref="OutboxMessage"/> to database/migrations/13_outbox_messages.sql.</summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(500).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredOn).HasColumnName("occurred_on");
        builder.Property(m => m.ProcessedOn).HasColumnName("processed_on");
        builder.Property(m => m.Error).HasColumnName("error");
        builder.Property(m => m.RetryCount).HasColumnName("retry_count");
    }
}
