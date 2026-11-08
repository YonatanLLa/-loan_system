using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Modules.Security.Core.Domain;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class SecurityDbContext : ModuleDbContext, ISecurityUnitOfWork, ISecurityOutbox
{
    // Enum labels in PostgreSQL are UPPER_SNAKE, so suppress the default snake-case translation.
    internal static readonly NpgsqlNullNameTranslator EnumNameTranslator = new();

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresEnum<SecurityRole>("public", "user_role", EnumNameTranslator);
        modelBuilder.HasPostgresEnum<UserStatus>("public", "user_status_type", EnumNameTranslator);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Email).HasColumnName("email");
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
            entity.Property(u => u.FullName).HasColumnName("full_name");
            entity.Property(u => u.Role).HasColumnName("role");
            entity.Property(u => u.Status).HasColumnName("status");
            entity.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(u => u.LockedUntil).HasColumnName("locked_until");
            entity.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            // High-volume technical rows (one per login/refresh): kept out of audit_logs (BE-SEC-07)
            entity.ExcludeFromAudit();
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id");
            entity.Property(t => t.UserId).HasColumnName("user_id");
            entity.Property(t => t.TokenHash).HasColumnName("token_hash");
            entity.Property(t => t.IssuedAt).HasColumnName("issued_at");
            entity.Property(t => t.ExpiresAt).HasColumnName("expires_at");
            entity.Property(t => t.RevokedAt).HasColumnName("revoked_at");
            entity.Property(t => t.UserIp).HasColumnName("user_ip");
            entity.Property(t => t.UserAgent).HasColumnName("user_agent");
        });
    }
}
