using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters.Outbound.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

/// <summary>
/// Atomic refresh-token rotation (DECISION_02): <c>revoked_at</c> is an optimistic concurrency token and
/// the unit of work surfaces lost races as the Core-level <see cref="ConcurrencyConflictException"/>.
/// The real UPDATE ... WHERE revoked_at IS NULL needs PostgreSQL (integration tests); here we check the
/// EF model and the exception translation without Docker.
/// </summary>
public class RefreshTokenConcurrencyTests
{
    [Fact]
    public void SecurityDbContext_ConfiguresRevokedAtAsConcurrencyTokenAndFamilyColumns()
    {
        // Building the model does not open a connection.
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options;
        using var db = new SecurityDbContext(options);

        var model = db.GetService<IDesignTimeModel>().Model;
        var refreshTokens = model.GetEntityTypes().Single(e => e.GetTableName() == "refresh_tokens");

        var revokedAt = refreshTokens.FindProperty("RevokedAt").ShouldNotBeNull();
        revokedAt.IsConcurrencyToken.ShouldBeTrue();
        revokedAt.GetColumnName().ShouldBe("revoked_at");

        refreshTokens.FindProperty("FamilyId").ShouldNotBeNull().GetColumnName().ShouldBe("family_id");
        refreshTokens.FindProperty("ReplacedByTokenId").ShouldNotBeNull().GetColumnName().ShouldBe("replaced_by_token_id");

        // Self-reference so EF inserts the new token before updating the old one that points to it.
        var replacedBy = refreshTokens.GetForeignKeys().Single();
        replacedBy.PrincipalEntityType.ShouldBe(refreshTokens);
        replacedBy.Properties.Single().Name.ShouldBe("ReplacedByTokenId");

        // Other columns must not be concurrency tokens (only revocation races matter).
        refreshTokens.GetProperties().Where(p => p.IsConcurrencyToken).ShouldHaveSingleItem().Name.ShouldBe("RevokedAt");
    }

    [Fact]
    public async Task SaveChanges_WhenConcurrencyTokenChangedByAnotherContext_ThrowsConcurrencyConflictException()
    {
        var databaseName = Guid.NewGuid().ToString();
        var rowId = Guid.NewGuid();

        await using (var seed = CreateContext(databaseName))
        {
            seed.Tokens.Add(new TestToken { Id = rowId });
            await seed.SaveChangesAsync();
        }

        await using var winner = CreateContext(databaseName);
        await using var loser = CreateContext(databaseName);
        var winnerRow = await winner.Tokens.SingleAsync(t => t.Id == rowId);
        var loserRow = await loser.Tokens.SingleAsync(t => t.Id == rowId);

        winnerRow.RevokedAt = DateTimeOffset.UtcNow;
        await winner.SaveChangesAsync();

        loserRow.RevokedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        IUnitOfWork unitOfWork = loser;
        var ex = await Should.ThrowAsync<ConcurrencyConflictException>(() => unitOfWork.SaveChangesAsync());
        ex.InnerException.ShouldBeOfType<DbUpdateConcurrencyException>();
    }

    private static TestModuleDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<TestModuleDbContext>().UseInMemoryDatabase(databaseName).Options);

    private sealed class TestToken
    {
        public Guid Id { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
    }

    private sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : ModuleDbContext(options)
    {
        public DbSet<TestToken> Tokens => Set<TestToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestToken>(b =>
            {
                b.HasKey(t => t.Id);
                b.Property(t => t.RevokedAt).IsConcurrencyToken();
                b.ExcludeFromAudit();
            });
        }
    }
}
