using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class AuditLogInterceptorApprovalTests
{
    private sealed class TestEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Role { get; set; } = "ROLE_ANALYST";
    }

    private sealed class TestApprovalEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Status { get; set; } = "PENDING";
    }

    private sealed class ApprovalTestDbContext(DbContextOptions<ApprovalTestDbContext> options) : ModuleDbContext(options)
    {
        public DbSet<TestEntity> Users => Set<TestEntity>();
        public DbSet<TestApprovalEntity> Approvals => Set<TestApprovalEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.ToTable("users");
                b.HasKey(u => u.Id);
            });
            modelBuilder.Entity<TestApprovalEntity>(b =>
            {
                b.ToTable("approval_requests");
                b.HasKey(a => a.Id);
            });
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    [Fact]
    public async Task SaveChanges_RecordsPerEntityActionsAndLinksApprovalRequestId()
    {
        var auditContext = new AuditContext();
        var interceptor = new AuditLogInterceptor(auditContext: auditContext, clock: new FixedClock());

        var options = new DbContextOptionsBuilder<ApprovalTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new ApprovalTestDbContext(options);
        var user = new TestEntity();
        var approval = new TestApprovalEntity();
        db.Users.Add(user);
        db.Approvals.Add(approval);
        await db.SaveChangesAsync();

        auditContext.SetApproval(approval.Id);
        auditContext.SetAction("APPROVE", "approval_requests");
        auditContext.SetAction("ROLE_CHANGE", "users");
        user.Role = "ROLE_OPERATOR";
        approval.Status = "APPROVED";

        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.Where(l => l.ApprovalRequestId == approval.Id).ToListAsync();
        logs.Count.ShouldBe(2);
        logs.ShouldContain(l => l.EntityName == "approval_requests" && l.Action == "APPROVE");
        logs.ShouldContain(l => l.EntityName == "users" && l.Action == "ROLE_CHANGE");
    }
}
