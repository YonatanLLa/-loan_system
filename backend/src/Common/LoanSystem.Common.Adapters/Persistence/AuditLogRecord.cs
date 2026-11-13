using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanSystem.Common.Adapters.Persistence;

public sealed class AuditLogRecord
{
    public long Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? StateBefore { get; set; }
    public string? StateAfter { get; set; }
    public Guid? UserId { get; set; }
    public IPAddress? UserIp { get; set; }
    public string? UserAgent { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Maps <see cref="AuditLogRecord"/> to database/migrations/02_security_users.sql, 14 and 17.</summary>
internal sealed class AuditLogRecordConfiguration : IEntityTypeConfiguration<AuditLogRecord>
{
    public void Configure(EntityTypeBuilder<AuditLogRecord> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(a => a.EntityName).HasColumnName("entity_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(a => a.StateBefore).HasColumnName("state_before").HasColumnType("jsonb");
        builder.Property(a => a.StateAfter).HasColumnName("state_after").HasColumnType("jsonb");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.UserIp).HasColumnName("user_ip");
        builder.Property(a => a.UserAgent).HasColumnName("user_agent");
        builder.Property(a => a.ApprovalRequestId).HasColumnName("approval_request_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
    }
}
