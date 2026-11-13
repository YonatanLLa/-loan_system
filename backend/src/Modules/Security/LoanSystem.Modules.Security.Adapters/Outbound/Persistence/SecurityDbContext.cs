using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Domain.Approvals;
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
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ApprovalRequestConfiguration());

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
            entity.Property(u => u.MfaEnabled).HasColumnName("mfa_enabled");
            entity.Property(u => u.MfaSecretEncrypted).HasColumnName("mfa_secret_encrypted");
            entity.Property(u => u.MfaEnrolledAt).HasColumnName("mfa_enrolled_at");
            entity.Property(u => u.MfaLastUsedStep).HasColumnName("mfa_last_used_step");
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

        modelBuilder.Entity<MfaChallenge>(entity =>
        {
            entity.ToTable("mfa_challenges");
            entity.ExcludeFromAudit();
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.UserId).HasColumnName("user_id");
            entity.Property(c => c.TokenHash).HasColumnName("token_hash");
            entity.Property(c => c.Purpose).HasColumnName("purpose").HasConversion<string>();
            entity.Property(c => c.Attempts).HasColumnName("attempts");
            entity.Property(c => c.ExpiresAt).HasColumnName("expires_at");
            entity.Property(c => c.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(c => c.UserIp).HasColumnName("user_ip");
            entity.Property(c => c.UserAgent).HasColumnName("user_agent");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<MfaRecoveryCode>(entity =>
        {
            entity.ToTable("mfa_recovery_codes");
            entity.ExcludeFromAudit();
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.UserId).HasColumnName("user_id");
            entity.Property(r => r.CodeHash).HasColumnName("code_hash");
            entity.Property(r => r.UsedAt).HasColumnName("used_at");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at");
        });
    }
}
