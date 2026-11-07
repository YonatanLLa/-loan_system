using System.Collections.Concurrent;
using Dapper;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Adapters.Outbox;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

public class OutboxIntegrationTests : IAsyncLifetime
{
    public sealed record LoanApproved(string LoanCode, decimal Amount) : IntegrationEvent;
    public sealed record UnhandledEvent(string Value) : IntegrationEvent;

    public sealed class ReceivedEvents
    {
        public ConcurrentBag<LoanApproved> Items { get; } = [];
    }

    private sealed class LoanApprovedHandler(ReceivedEvents received) : IIntegrationEventHandler<LoanApproved>
    {
        public Task HandleAsync(LoanApproved integrationEvent, CancellationToken ct = default)
        {
            received.Items.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    // Same base as every module DbContext: outbox rows commit with the module's data
    private sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : ModuleDbContext(options);

    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_outbox_test")
                .WithUsername("postgres")
                .WithPassword("postgrespassword")
                .Build();

            await _postgresContainer.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Docker not available or failed to start: {ex.Message}");
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task EnqueuedEvents_AreDispatchedToHandlers_AndUnknownTypesAreParked()
    {
        if (!_dockerAvailable || _postgresContainer is null)
        {
            // Skip assertion if Docker is not active on host machine
            return;
        }

        var connectionString = _postgresContainer.GetConnectionString();
        var migration = LoanSystem.DbMigrator.Program.Upgrade(connectionString, LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!);
        migration.Successful.ShouldBeTrue($"DbMigrator failed with: {migration.Error}");

        // 1. A module writes its events through the outbox port in the same SaveChanges
        var handled = new LoanApproved("PR-0001", 10000m);
        var unhandled = new UnhandledEvent("nobody listens");
        var dbOptions = new DbContextOptionsBuilder<TestModuleDbContext>().UseNpgsql(connectionString).Options;
        await using (var dbContext = new TestModuleDbContext(dbOptions))
        {
            IOutbox outbox = dbContext;
            outbox.Enqueue(handled);
            outbox.Enqueue(unhandled);
            await dbContext.SaveChangesAsync();
        }

        // 2. The Workers host wiring processes one batch
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ReceivedEvents>();
        services.AddCommonPersistence(configuration);
        services.AddIntegrationEventHandler<LoanApproved, LoanApprovedHandler>();

        await using var provider = services.BuildServiceProvider();
        var processed = await provider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync();

        // 3. Handler received the deserialized event and rows were updated
        processed.ShouldBe(2);
        var received = provider.GetRequiredService<ReceivedEvents>().Items.ShouldHaveSingleItem();
        received.EventId.ShouldBe(handled.EventId);
        received.LoanCode.ShouldBe("PR-0001");
        received.Amount.ShouldBe(10000m);

        await using var connection = new NpgsqlConnection(connectionString);
        var handledRow = await connection.QuerySingleAsync<(DateTimeOffset? ProcessedOn, int RetryCount)>(
            "SELECT processed_on, retry_count FROM outbox_messages WHERE id = @Id", new { Id = handled.EventId });
        handledRow.ProcessedOn.ShouldNotBeNull();
        handledRow.RetryCount.ShouldBe(0);

        var unhandledRow = await connection.QuerySingleAsync<(DateTimeOffset? ProcessedOn, int RetryCount, string Error)>(
            "SELECT processed_on, retry_count, error FROM outbox_messages WHERE id = @Id", new { Id = unhandled.EventId });
        unhandledRow.ProcessedOn.ShouldBeNull();
        unhandledRow.RetryCount.ShouldBe(OutboxProcessor.MaxRetries);
        unhandledRow.Error.ShouldContain("Unknown event type");

        // 4. Nothing is picked up again
        (await provider.GetRequiredService<OutboxProcessor>().ProcessBatchAsync()).ShouldBe(0);
    }
}
