using Dapper;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

/// <summary>
/// Refresh-token families, reuse detection and atomic rotation against a real PostgreSQL with
/// migration 16 applied (DECISION_02). Drives the public use-case ports through the real Security module wiring.
/// </summary>
public class RefreshTokenRotationIntegrationTests : IAsyncLifetime
{
    private const string Password = "Integration-Pass-1";

    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_refresh_test")
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
    public async Task ReusingARotatedToken_RevokesItsWholeFamily_AndLeavesOtherSessionsActive()
    {
        if (!_dockerAvailable || _postgresContainer is null)
            return;

        var (provider, connectionString) = await PrepareAsync();
        var email = await InsertUserAsync(connectionString);

        var session = await LoginAsync(provider, email);
        var otherDevice = await LoginAsync(provider, email);

        var rotated = await RefreshAsync(provider, session.RefreshToken);
        rotated.IsSuccess.ShouldBeTrue();

        var reuse = await RefreshAsync(provider, session.RefreshToken);
        reuse.IsFailure.ShouldBeTrue();
        reuse.Error.Code.ShouldBe("Security.RefreshTokenReused");

        await using var connection = new NpgsqlConnection(connectionString);
        var familyId = await FamilyOfAsync(connection, session.RefreshToken);
        (await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM refresh_tokens WHERE family_id = @familyId", new { familyId })).ShouldBe(2);
        (await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM refresh_tokens WHERE family_id = @familyId AND revoked_at IS NULL", new { familyId })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<Guid?>(
            "SELECT replaced_by_token_id FROM refresh_tokens WHERE id = @familyId", new { familyId })).ShouldNotBeNull();

        var otherFamily = await FamilyOfAsync(connection, otherDevice.RefreshToken);
        (await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM refresh_tokens WHERE family_id = @otherFamily AND revoked_at IS NULL", new { otherFamily })).ShouldBe(1);

        (await RefreshAsync(provider, rotated.Value.RefreshToken)).Error.Code.ShouldBe("Security.InvalidRefreshToken");
        (await RefreshAsync(provider, otherDevice.RefreshToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrentRefreshesWithTheSameToken_NeverLeaveTwoActiveTokensInTheFamily()
    {
        if (!_dockerAvailable || _postgresContainer is null)
            return;

        var (provider, connectionString) = await PrepareAsync();
        var email = await InsertUserAsync(connectionString);
        var session = await LoginAsync(provider, email);

        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => RefreshAsync(provider, session.RefreshToken))));

        // At most one request wins the conditional UPDATE; the rest get 401 (lost race or detected reuse).
        results.Count(r => r.IsSuccess).ShouldBeLessThanOrEqualTo(1);
        results.Where(r => r.IsFailure).ShouldAllBe(r => r.Error.Type == ErrorType.Unauthorized);

        await using var connection = new NpgsqlConnection(connectionString);
        var familyId = await FamilyOfAsync(connection, session.RefreshToken);
        (await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM refresh_tokens WHERE family_id = @familyId AND revoked_at IS NULL", new { familyId }))
            .ShouldBeLessThanOrEqualTo(1);
        (await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM refresh_tokens WHERE family_id = @familyId AND replaced_by_token_id IS NOT NULL", new { familyId }))
            .ShouldBeLessThanOrEqualTo(1);
    }

    private async Task<(ServiceProvider Provider, string ConnectionString)> PrepareAsync()
    {
        var connectionString = _postgresContainer!.GetConnectionString();
        var migration = LoanSystem.DbMigrator.Program.Upgrade(connectionString, LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!);
        migration.Successful.ShouldBeTrue($"DbMigrator failed with: {migration.Error}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["Jwt:Key"] = "IntegrationTestsSigningKeyThatIsLongEnoughForHs256-0123456789"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, AnonymousUser>();
        services.AddSecurityModule(configuration);

        return (services.BuildServiceProvider(), connectionString);
    }

    private static async Task<string> InsertUserAsync(string connectionString)
    {
        var userId = Guid.NewGuid();
        var email = $"refresh-{userId:N}@loan.test";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(
            """
            INSERT INTO users (id, email, password_hash, full_name, role)
            VALUES (@Id, @Email, @Hash, 'Refresh Integration', 'ROLE_ANALYST')
            """,
            new { Id = userId, Email = email, Hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4) });
        return email;
    }

    private static async Task<AuthTokensResponse> LoginAsync(ServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ILoginUseCase>()
            .ExecuteAsync(new LoginRequest(email, Password));
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    // One DI scope (= one DbContext) per request, like the real HTTP pipeline.
    private static async Task<Result<AuthTokensResponse>> RefreshAsync(ServiceProvider provider, string rawToken)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenUseCase>()
            .ExecuteAsync(new RefreshRequest(rawToken));
    }

    // family_id of the token issued at login equals its own id.
    private static Task<Guid> FamilyOfAsync(NpgsqlConnection connection, string rawToken) =>
        connection.ExecuteScalarAsync<Guid>(
            "SELECT family_id FROM refresh_tokens WHERE token_hash = @hash",
            new { hash = HashLikeTheModule(rawToken) });

    private static string HashLikeTheModule(string rawToken)
    {
        // Mirrors Security.Core RefreshTokenHasher (internal to the module).
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    private sealed class AnonymousUser : ICurrentUser
    {
        public Guid? UserId => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public Guid? CustomerId => null;
        public bool IsAuthenticated => false;
        public bool IsInRole(string role) => false;
    }
}
