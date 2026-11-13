using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OtpNet;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace LoanSystem.IntegrationTests;

[CollectionDefinition(nameof(WebApiStartupCollection), DisableParallelization = true)]
public sealed class WebApiStartupCollection;

[Collection(nameof(WebApiStartupCollection))]
public class JwtRotationAndSecretsIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private bool _dockerAvailable = true;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("loan_system_jwt_test")
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
    public void ProductionStartup_WithoutJwtSigningKeys_ThrowsInvalidOperationException()
    {
        using var envScope = WithEnvironmentVariables(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Secrets__Provider"] = "None",
            ["ConnectionStrings__Database"] = "Host=prod-db;Database=loans;Username=svc;Password=StrongProdPassword!1",
            ["Security__Mfa__EncryptionKeys"] = ValidMfaKeysJson(),
            ["Jwt__SigningKeys"] = null,
            ["Jwt__Key"] = null
        });

        using var factory = new WebApplicationFactory<Program>();
        Should.Throw<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void ProductionStartup_WithDevDatabasePassword_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        using var envScope = WithEnvironmentVariables(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Secrets__Provider"] = "None",
            ["ConnectionStrings__Database"] = "Host=localhost;Database=loans;Username=postgres;Password=postgrespassword",
            ["Security__Mfa__EncryptionKeys"] = ValidMfaKeysJson(),
            ["Jwt__SigningKeys"] = BuildSigningKeysJson((rsa, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30))),
            ["Jwt__Key"] = null
        });

        using var factory = new WebApplicationFactory<Program>();
        Should.Throw<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task DevelopmentStartup_WithoutJwtSigningKeys_BootsWithEphemeralKeyAndServesJwks()
    {
        using var envScope = WithEnvironmentVariables(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Secrets__Provider"] = "None",
            ["ConnectionStrings__Redis"] = "",
            ["Jwt__SigningKeys"] = null,
            ["Jwt__Key"] = null
        });

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var jwksResponse = await client.GetAsync("/.well-known/jwks.json");
        jwksResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        jwksResponse.Headers.CacheControl?.ToString().ShouldBe("public, max-age=300");

        var body = await jwksResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var keys = doc.RootElement.GetProperty("keys");
        keys.GetArrayLength().ShouldBe(1);
        keys[0].GetProperty("alg").GetString().ShouldBe("RS256");
        keys[0].TryGetProperty("d", out _).ShouldBeFalse();

        var healthResponse = await client.GetAsync("/health");
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var healthJson = await healthResponse.Content.ReadAsStringAsync();
        healthJson.ShouldContain("Healthy");
    }

    [Fact]
    public async Task EndToEnd_LoginWithMfaV2_Rs256AccessToken_KeyPerFileRotation_AndRevocation()
    {
        if (!_dockerAvailable || _postgresContainer is null) return;

        var cs = _postgresContainer.GetConnectionString();
        var migration = LoanSystem.DbMigrator.Program.Upgrade(cs, LoanSystem.DbMigrator.Program.FindDatabaseDirectory()!);
        migration.Successful.ShouldBeTrue($"DbMigrator failed with: {migration.Error}");

        var tempSecretsDir = Path.Combine(Path.GetTempPath(), $"loansystem-it-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSecretsDir);

        try
        {
            using var rsa1 = RSA.Create(2048);
            using var rsa2 = RSA.Create(2048);
            var key1Json = BuildSigningKeysJson((rsa1, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30)));
            var mfaJson = ValidMfaKeysJson();

            await File.WriteAllTextAsync(Path.Combine(tempSecretsDir, "ConnectionStrings__Database"), cs);
            await File.WriteAllTextAsync(Path.Combine(tempSecretsDir, "Jwt__SigningKeys"), key1Json);
            await File.WriteAllTextAsync(Path.Combine(tempSecretsDir, "Security__Mfa__EncryptionKeys"), mfaJson);

            // Seed an active analyst (no MFA required) and an admin for MFA flow
            var userId = Guid.NewGuid();
            var email = "analyst.jwt@loan.test";
            const string password = "StrongUserPass!2026";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO users (id, email, password_hash, full_name, role, status)
                    VALUES (@Id, @Email, @Hash, 'JWT Analyst', 'ROLE_ANALYST', 'ACTIVE')
                    """,
                    new { Id = userId, Email = email, Hash = passwordHash });
            }

            using var envScope = WithEnvironmentVariables(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Secrets__Provider"] = "KeyPerFile",
                ["Secrets__Path"] = tempSecretsDir,
                ["Secrets__ReloadInterval"] = "00:00:00.200",
                ["Jwt__SigningKeys"] = null,
                ["Jwt__Key"] = null,
                ["Security__Mfa__EncryptionKeys"] = null
            });

            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // 1. Login -> RS256 token with K1 kid -> /api/me 200
            var login1 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login1.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var login1Doc = JsonDocument.Parse(await login1.Content.ReadAsStringAsync());
            var token1 = login1Doc.RootElement.GetProperty("accessToken").GetString()!;
            var jwt1 = new JwtSecurityTokenHandler().ReadJwtToken(token1);
            jwt1.Header.Alg.ShouldBe("RS256");
            var kid1 = jwt1.Header.Kid;

            using (var meReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/me"))
            {
                meReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
                var meRes1 = await client.SendAsync(meReq1);
                meRes1.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            // 2. Rotate KeyPerFile with K1 + newer K2
            var key2Json = BuildSigningKeysJson(
                (rsa1, DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow.AddDays(10)),
                (rsa2, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(90)));
            await File.WriteAllTextAsync(Path.Combine(tempSecretsDir, "Jwt__SigningKeys"), key2Json);

            string token2 = string.Empty;
            string kid2 = string.Empty;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var jwksDoc = JsonDocument.Parse(await client.GetStringAsync("/.well-known/jwks.json"));
                if (jwksDoc.RootElement.GetProperty("keys").GetArrayLength() == 2)
                {
                    var login2 = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
                    login2.StatusCode.ShouldBe(HttpStatusCode.OK);
                    using var login2Doc = JsonDocument.Parse(await login2.Content.ReadAsStringAsync());
                    token2 = login2Doc.RootElement.GetProperty("accessToken").GetString()!;
                    kid2 = new JwtSecurityTokenHandler().ReadJwtToken(token2).Header.Kid;
                    if (kid2 != kid1)
                    {
                        break;
                    }
                }

                await Task.Delay(100);
            }

            kid2.ShouldNotBe(kid1);

            // Both K1 and K2 tokens remain accepted during rotation overlap
            using (var checkOld = new HttpRequestMessage(HttpMethod.Get, "/api/me"))
            {
                checkOld.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
                (await client.SendAsync(checkOld)).StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            using (var checkNew = new HttpRequestMessage(HttpMethod.Get, "/api/me"))
            {
                checkNew.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
                (await client.SendAsync(checkNew)).StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            // 3. Remove K1 completely -> old token immediately rejected (401), K2 still valid (200)
            var revokedJson = BuildSigningKeysJson((rsa2, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(90)));
            await File.WriteAllTextAsync(Path.Combine(tempSecretsDir, "Jwt__SigningKeys"), revokedJson);

            HttpStatusCode oldStatusAfterRevoke = HttpStatusCode.OK;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "/api/me");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
                oldStatusAfterRevoke = (await client.SendAsync(req)).StatusCode;
                if (oldStatusAfterRevoke == HttpStatusCode.Unauthorized)
                {
                    break;
                }

                await Task.Delay(100);
            }

            oldStatusAfterRevoke.ShouldBe(HttpStatusCode.Unauthorized);

            using (var finalNew = new HttpRequestMessage(HttpMethod.Get, "/api/me"))
            {
                finalNew.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
                (await client.SendAsync(finalNew)).StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            // 4. Verify MFA v2 encryption persists v2:{keyId}: format on admin enrollment
            var adminId = Guid.NewGuid();
            const string adminEmail = "admin.mfa@loan.test";
            await using (var conn = new NpgsqlConnection(cs))
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO users (id, email, password_hash, full_name, role, status)
                    VALUES (@Id, @Email, @Hash, 'MFA Admin', 'ROLE_ADMIN', 'ACTIVE')
                    """,
                    new { Id = adminId, Email = adminEmail, Hash = passwordHash });
            }

            var adminLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = adminEmail, password });
            adminLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var adminLoginDoc = JsonDocument.Parse(await adminLogin.Content.ReadAsStringAsync());
            adminLoginDoc.RootElement.GetProperty("mfaEnrollmentRequired").GetBoolean().ShouldBeTrue();
            var mfaToken = adminLoginDoc.RootElement.GetProperty("mfaToken").GetString()!;

            var beginEnroll = await client.PostAsJsonAsync("/api/auth/mfa/enroll", new { mfaToken });
            beginEnroll.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var enrollDoc = JsonDocument.Parse(await beginEnroll.Content.ReadAsStringAsync());
            var totpSecret = enrollDoc.RootElement.GetProperty("secret").GetString()!;

            var totpCode = new Totp(Base32Encoding.ToBytes(totpSecret)).ComputeTotp();
            var confirmEnroll = await client.PostAsJsonAsync("/api/auth/mfa/enroll/confirm", new { mfaToken, code = totpCode });
            confirmEnroll.StatusCode.ShouldBe(HttpStatusCode.OK);

            await using (var conn = new NpgsqlConnection(cs))
            {
                var storedCiphertext = await conn.ExecuteScalarAsync<string>(
                    "SELECT mfa_secret_encrypted FROM users WHERE id = @Id", new { Id = adminId });
                storedCiphertext.ShouldStartWith("v2:2026-11:");
            }
        }
        finally
        {
            if (Directory.Exists(tempSecretsDir))
            {
                Directory.Delete(tempSecretsDir, recursive: true);
            }
        }
    }

    private static string BuildSigningKeysJson(params (RSA Key, DateTimeOffset NotBefore, DateTimeOffset NotAfter)[] keys) =>
        JsonSerializer.Serialize(new
        {
            keys = keys.Select(k => new
            {
                privateKeyPem = k.Key.ExportPkcs8PrivateKeyPem(),
                notBefore = k.NotBefore,
                notAfter = k.NotAfter
            })
        });

    private static string ValidMfaKeysJson() =>
        JsonSerializer.Serialize(new
        {
            active = "2026-11",
            keys = new Dictionary<string, string>
            {
                ["2026-11"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            }
        });

    private static IDisposable WithEnvironmentVariables(Dictionary<string, string?> values)
    {
        var previous = new Dictionary<string, string?>(values.Count, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        return new EnvRestoreScope(previous);
    }

    private sealed class EnvRestoreScope(Dictionary<string, string?> previous) : IDisposable
    {
        public void Dispose()
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
