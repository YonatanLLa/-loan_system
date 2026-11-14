using LoanSystem.Common.Core.Domain.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>
/// Opt-in EF Core configuration for <see cref="ApprovalRequest"/> (<c>database/migrations/18_approval_requests.sql</c>).
/// Modules that submit or handle four-eyes approvals apply this configuration in their <see cref="ModuleDbContext"/>.
/// </summary>
public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OperationType).HasColumnName("operation_type").HasMaxLength(40).IsRequired();
        builder.Property(a => a.Module).HasColumnName("module").HasMaxLength(30).IsRequired();
        builder.Property(a => a.TargetEntity).HasColumnName("target_entity").HasMaxLength(50).IsRequired();
        builder.Property(a => a.TargetId).HasColumnName("target_id");
        builder.Property(a => a.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.TargetSnapshotJson).HasColumnName("target_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.Reason).HasColumnName("reason").IsRequired();
        builder.Property(a => a.EvidenceRef).HasColumnName("evidence_ref").HasMaxLength(100);
        builder.Property(a => a.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(a => a.RequestedBy).HasColumnName("requested_by");
        builder.Property(a => a.RequestedAt).HasColumnName("requested_at");
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at");
        builder.Property(a => a.DecidedBy).HasColumnName("decided_by");
        builder.Property(a => a.DecidedAt).HasColumnName("decided_at");
        builder.Property(a => a.DecisionComment).HasColumnName("decision_comment");

        // Optimistic concurrency via PostgreSQL xmin system column (D9)
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Ignore(a => a.DomainEvents);
    }
}
