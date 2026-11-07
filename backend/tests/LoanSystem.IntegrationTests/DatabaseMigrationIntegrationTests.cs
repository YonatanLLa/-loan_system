using Dapper;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

public class DatabaseMigrationIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_test")
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
    public async Task DbMigrator_ShouldApplyAllMigrations_AndCreateCoreTables()
    {
        if (!_dockerAvailable || _postgresContainer is null)
        {
            // Skip assertion if Docker is not active on host machine
            return;
        }

        var connectionString = _postgresContainer.GetConnectionString();

        var databaseDir = LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!;
        databaseDir.ShouldNotBeNull();

        // 1. Run DbMigrator Upgrade
        var result = LoanSystem.DbMigrator.Program.Upgrade(connectionString, databaseDir);
        result.Successful.ShouldBeTrue($"DbMigrator failed with: {result.Error}");

        // 2. Query PostgreSQL to verify that tables exist
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = (await connection.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
            .ToHashSet();

        // Verify core module tables created by existing SQL migrations
        tables.ShouldContain("users");
        tables.ShouldContain("customers");
        tables.ShouldContain("customer_bank_accounts");
        tables.ShouldContain("loan_products");
        tables.ShouldContain("loan_applications");
        tables.ShouldContain("loans");
        tables.ShouldContain("schedules");
        tables.ShouldContain("installments");
        tables.ShouldContain("payments");
        tables.ShouldContain("transactions");
        tables.ShouldContain("company_bank_accounts");
        tables.ShouldContain("disbursements");
        tables.ShouldContain("bank_statements");
        tables.ShouldContain("collection_actions");
        tables.ShouldContain("integration_events");
        tables.ShouldContain("outbox_messages");

        // Verify seed data was loaded
        int userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
        userCount.ShouldBeGreaterThan(0, "Seed data should have created users");
    }
}
