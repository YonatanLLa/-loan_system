using System.Net;
using System.Text.Json;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class AuditLogInterceptorTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public IReadOnlyCollection<string> Roles { get; set; } = [];
        public Guid? CustomerId { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    private sealed class TestProduct
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private sealed class TestAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? SecretToken { get; set; }
    }

    private sealed class TestSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TokenHash { get; set; } = string.Empty;
    }

    private sealed class TestCredential
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset TokenExpiresAt { get; set; }
        public string? ApiSecret { get; set; }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTime LimaNow => now.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class TestModuleDbContext : ModuleDbContext
    {
        public TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : base(options) { }

        public DbSet<TestProduct> Products => Set<TestProduct>();
        public DbSet<TestAccount> Accounts => Set<TestAccount>();
        public DbSet<TestSession> Sessions => Set<TestSession>();
        public DbSet<TestCredential> Credentials => Set<TestCredential>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestProduct>(b =>
            {
                b.ToTable("test_products");
                b.HasKey(p => p.Id);
                b.Property(p => p.Name).IsRequired();
                b.Property(p => p.Price);
            });

            modelBuilder.Entity<TestAccount>(b =>
            {
                b.ToTable("test_accounts");
                b.HasKey(a => a.Id);
                b.Property(a => a.Username).IsRequired();
                b.Property(a => a.Password).IsRequired();
                b.Property(a => a.PasswordHash).IsRequired();
                b.Property(a => a.SecretToken);
            });

            modelBuilder.Entity<TestSession>(b =>
            {
                b.ToTable("test_sessions");
                b.ExcludeFromAudit();
                b.HasKey(x => x.Id);
            });

            modelBuilder.Entity<TestCredential>(b =>
            {
                b.ToTable("test_credentials");
                b.HasKey(x => x.Id);
            });
        }
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(IPAddress? ip, string? userAgent)
    {
        var context = new DefaultHttpContext();
        if (ip is not null)
        {
            context.Connection.RemoteIpAddress = ip;
        }
        if (userAgent is not null)
        {
            context.Request.Headers.UserAgent = userAgent;
        }
        return new HttpContextAccessor { HttpContext = context };
    }

    private static TestModuleDbContext CreateDbContext(AuditLogInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestModuleDbContext(options);
    }

    [Fact]
    public async Task AddEntity_ProducesAuditLog_WithCreateActionAndStateAfter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { UserId = userId };
        var httpContextAccessor = CreateHttpContextAccessor(IPAddress.Parse("192.168.1.100"), "Mozilla/5.0 TestBrowser");
        var interceptor = new AuditLogInterceptor(currentUser, httpContextAccessor);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct
        {
            Name = "Préstamo Personal",
            Price = 5000m
        };

        // Act
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Assert
        var logs = await context.AuditLogs.ToListAsync();
        logs.Count.ShouldBe(1);

        var log = logs[0];
        log.Action.ShouldBe("CREATE");
        log.EntityName.ShouldBe("test_products");
        log.EntityId.ShouldBe(product.Id.ToString());
        log.StateBefore.ShouldBeNull();
        log.StateAfter.ShouldNotBeNull();
        log.StateAfter.ShouldContain("\"Name\":\"Préstamo Personal\"");
        log.StateAfter.ShouldContain("\"Price\":5000");
        log.UserId.ShouldBe(userId);
        log.UserIp.ShouldBe(IPAddress.Parse("192.168.1.100"));
        log.UserAgent.ShouldBe("Mozilla/5.0 TestBrowser");
    }

    [Fact]
    public async Task ModifyEntity_ProducesAuditLog_WithUpdateActionAndBeforeAfter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { UserId = userId };
        var httpContextAccessor = CreateHttpContextAccessor(IPAddress.Parse("10.0.0.1"), "LoanApp/1.0");
        var interceptor = new AuditLogInterceptor(currentUser, httpContextAccessor);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct
        {
            Name = "Initial Name",
            Price = 100m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Act
        product.Name = "Updated Name";
        product.Price = 200m;
        await context.SaveChangesAsync();

        // Assert
        var logs = await context.AuditLogs.OrderBy(l => l.Id).ToListAsync();
        logs.Count.ShouldBe(2);

        var updateLog = logs[1];
        updateLog.Action.ShouldBe("UPDATE");
        updateLog.EntityName.ShouldBe("test_products");
        updateLog.EntityId.ShouldBe(product.Id.ToString());
        updateLog.StateBefore.ShouldNotBeNull();
        updateLog.StateBefore.ShouldContain("\"Name\":\"Initial Name\"");
        updateLog.StateBefore.ShouldContain("\"Price\":100");
        updateLog.StateAfter.ShouldNotBeNull();
        updateLog.StateAfter.ShouldContain("\"Name\":\"Updated Name\"");
        updateLog.StateAfter.ShouldContain("\"Price\":200");
        updateLog.UserId.ShouldBe(userId);
        updateLog.UserIp.ShouldBe(IPAddress.Parse("10.0.0.1"));
        updateLog.UserAgent.ShouldBe("LoanApp/1.0");
    }

    [Fact]
    public async Task DeleteEntity_ProducesAuditLog_WithDeleteActionAndStateBefore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { UserId = userId };
        var httpContextAccessor = CreateHttpContextAccessor(IPAddress.Loopback, "curl/7.68.0");
        var interceptor = new AuditLogInterceptor(currentUser, httpContextAccessor);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct
        {
            Name = "To Delete",
            Price = 50m
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Act
        context.Products.Remove(product);
        await context.SaveChangesAsync();

        // Assert
        var logs = await context.AuditLogs.OrderBy(l => l.Id).ToListAsync();
        logs.Count.ShouldBe(2);

        var deleteLog = logs[1];
        deleteLog.Action.ShouldBe("DELETE");
        deleteLog.EntityName.ShouldBe("test_products");
        deleteLog.EntityId.ShouldBe(product.Id.ToString());
        deleteLog.StateBefore.ShouldNotBeNull();
        deleteLog.StateBefore.ShouldContain("\"Name\":\"To Delete\"");
        deleteLog.StateAfter.ShouldBeNull();
        deleteLog.UserId.ShouldBe(userId);
        deleteLog.UserIp.ShouldBe(IPAddress.Loopback);
        deleteLog.UserAgent.ShouldBe("curl/7.68.0");
    }

    [Fact]
    public async Task OutboxMessageAndAuditLogRecord_DoNotProduceCircularAuditLogs()
    {
        // Arrange
        var interceptor = new AuditLogInterceptor();
        await using var context = CreateDbContext(interceptor);

        // Act: Save OutboxMessage and AuditLogRecord directly
        context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "TestEvent",
            Payload = "{}"
        });

        context.Set<AuditLogRecord>().Add(new AuditLogRecord
        {
            EntityName = "manual_entry",
            EntityId = "999",
            Action = "MANUAL_ACTION"
        });

        await context.SaveChangesAsync();

        // Assert: Exactly 1 record should exist (the manually added one), and no circular audit logs
        var logs = await context.AuditLogs.ToListAsync();
        logs.Count.ShouldBe(1);
        logs[0].EntityName.ShouldBe("manual_entry");
        logs[0].Action.ShouldBe("MANUAL_ACTION");
    }

    [Fact]
    public async Task SensitiveFields_AreMaskedAsRedacted()
    {
        // Arrange
        var interceptor = new AuditLogInterceptor();
        await using var context = CreateDbContext(interceptor);

        var account = new TestAccount
        {
            Username = "johndoe",
            Password = "MySecretPassword123!",
            PasswordHash = "$2a$12$e80yq9gqg2qQ9H1vWb...",
            SecretToken = "eyJhGciOiJIUz..."
        };

        // Act
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        // Assert
        var log = await context.AuditLogs.SingleAsync();
        log.StateAfter.ShouldNotBeNull();

        log.StateAfter.ShouldContain("\"Username\":\"johndoe\"");
        log.StateAfter.ShouldContain("\"Password\":\"[REDACTED]\"");
        log.StateAfter.ShouldContain("\"PasswordHash\":\"[REDACTED]\"");
        log.StateAfter.ShouldContain("\"SecretToken\":\"[REDACTED]\"");

        log.StateAfter.ShouldNotContain("MySecretPassword123!");
        log.StateAfter.ShouldNotContain("$2a$12$e80yq9gqg2qQ9H1vWb...");
        log.StateAfter.ShouldNotContain("eyJhGciOiJIUz...");
    }

    [Fact]
    public async Task EmptyChanges_DoesNotCreateAuditLogs_OrThrow()
    {
        // Arrange
        var interceptor = new AuditLogInterceptor();
        await using var context = CreateDbContext(interceptor);

        // Act
        var result = await context.SaveChangesAsync();

        // Assert
        result.ShouldBe(0);
        var logs = await context.AuditLogs.ToListAsync();
        logs.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnonymousUserAndNullHttpContext_SavesAuditLogWithNullContextInfo()
    {
        // Arrange
        var interceptor = new AuditLogInterceptor(currentUser: null, httpContextAccessor: null);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct { Name = "Unauthenticated Product", Price = 10m };

        // Act
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Assert
        var log = await context.AuditLogs.SingleAsync();
        log.Action.ShouldBe("CREATE");
        log.UserId.ShouldBeNull();
        log.UserIp.ShouldBeNull();
        log.UserAgent.ShouldBeNull();
    }

    [Fact]
    public async Task BusinessAction_FromAuditContext_ReplacesGenericActionForAddedAndModified()
    {
        var auditContext = new AuditContext();
        var interceptor = new AuditLogInterceptor(auditContext: auditContext);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct { Name = "Original", Price = 1m };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        auditContext.SetAction("approve");
        product.Name = "Approved";
        await context.SaveChangesAsync();

        var logs = await context.AuditLogs.OrderBy(l => l.Id).ToListAsync();
        logs[0].Action.ShouldBe("CREATE");
        logs[1].Action.ShouldBe("APPROVE"); // normalized to upper case
    }

    [Fact]
    public async Task BusinessAction_DoesNotHidePhysicalDelete()
    {
        var auditContext = new AuditContext();
        var interceptor = new AuditLogInterceptor(auditContext: auditContext);
        await using var context = CreateDbContext(interceptor);

        var product = new TestProduct { Name = "Temp", Price = 1m };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        auditContext.SetAction("REVERSE");
        context.Products.Remove(product);
        await context.SaveChangesAsync();

        var deleteLog = (await context.AuditLogs.OrderBy(l => l.Id).ToListAsync())[1];
        deleteLog.Action.ShouldBe("DELETE");
    }

    [Fact]
    public async Task Actor_IsUsed_WhenRequestIsAnonymous()
    {
        var actorId = Guid.NewGuid();
        var auditContext = new AuditContext();
        auditContext.SetActor(actorId);
        var interceptor = new AuditLogInterceptor(new TestCurrentUser { UserId = null }, auditContext: auditContext);
        await using var context = CreateDbContext(interceptor);

        context.Products.Add(new TestProduct { Name = "Login", Price = 0m });
        await context.SaveChangesAsync();

        (await context.AuditLogs.SingleAsync()).UserId.ShouldBe(actorId);
    }

    [Fact]
    public async Task AuthenticatedUser_TakesPrecedenceOverActor()
    {
        var authenticatedId = Guid.NewGuid();
        var auditContext = new AuditContext();
        auditContext.SetActor(Guid.NewGuid());
        var interceptor = new AuditLogInterceptor(new TestCurrentUser { UserId = authenticatedId }, auditContext: auditContext);
        await using var context = CreateDbContext(interceptor);

        context.Products.Add(new TestProduct { Name = "X", Price = 0m });
        await context.SaveChangesAsync();

        (await context.AuditLogs.SingleAsync()).UserId.ShouldBe(authenticatedId);
    }

    [Fact]
    public async Task EntityExcludedFromAudit_ProducesNoAuditLog()
    {
        var interceptor = new AuditLogInterceptor();
        await using var context = CreateDbContext(interceptor);

        context.Sessions.Add(new TestSession { TokenHash = "abc" });
        context.Products.Add(new TestProduct { Name = "Audited", Price = 1m });
        await context.SaveChangesAsync();

        var log = await context.AuditLogs.SingleAsync();
        log.EntityName.ShouldBe("test_products");
        (await context.Sessions.CountAsync()).ShouldBe(1); // the excluded row itself is still saved
    }

    [Fact]
    public async Task CreatedAt_ComesFromClock()
    {
        var now = new DateTimeOffset(2026, 11, 8, 15, 30, 0, TimeSpan.Zero);
        var interceptor = new AuditLogInterceptor(clock: new FixedClock(now));
        await using var context = CreateDbContext(interceptor);

        context.Products.Add(new TestProduct { Name = "Clocked", Price = 1m });
        await context.SaveChangesAsync();

        (await context.AuditLogs.SingleAsync()).CreatedAt.ShouldBe(now);
    }

    [Fact]
    public async Task SensitiveFilter_MatchesSpecificNames_NotEveryTokenSubstring()
    {
        var interceptor = new AuditLogInterceptor();
        await using var context = CreateDbContext(interceptor);

        context.Credentials.Add(new TestCredential
        {
            TokenExpiresAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ApiSecret = "super-secret-value"
        });
        await context.SaveChangesAsync();

        var stateAfter = (await context.AuditLogs.SingleAsync()).StateAfter!;
        stateAfter.ShouldContain("\"TokenExpiresAt\":\"2030-01-01");
        stateAfter.ShouldContain("\"ApiSecret\":\"[REDACTED]\"");
        stateAfter.ShouldNotContain("super-secret-value");
    }

    [Fact]
    public void AddAuditLogging_ResolvesInterceptor_WithoutHttpOrCurrentUser()
    {
        // Same situation as the Workers host: no ICurrentUser, no IHttpContextAccessor, no IClock
        var services = new ServiceCollection();
        services.AddAuditLogging();
        services.AddAuditLogging(); // idempotent: every module calls it

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AuditLogInterceptor>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().ShouldBeOfType<AuditContext>();
        services.Count(d => d.ServiceType == typeof(AuditLogInterceptor)).ShouldBe(1);
    }
}
