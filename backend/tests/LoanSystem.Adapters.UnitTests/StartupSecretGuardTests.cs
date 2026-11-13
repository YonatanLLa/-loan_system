using System.Security.Cryptography;
using System.Text.Json;
using LoanSystem.Common.Adapters.Configuration;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class StartupSecretGuardTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTime LimaNow => DateTime.UtcNow.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private static string ValidSigningKeysJson(RSA key) =>
        JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    privateKeyPem = key.ExportPkcs8PrivateKeyPem(),
                    notBefore = DateTimeOffset.UtcNow.AddMinutes(-5),
                    notAfter = DateTimeOffset.UtcNow.AddDays(30)
                }
            }
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

    [Fact]
    public void ValidateStartupSecrets_Production_RejectsMissingOrDevPlaceholderSecrets()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var rsa = RSA.Create(2048);

        var validBase = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=prod-db;Database=loans;Username=svc;Password=StrongProdPassword!99",
            ["Jwt:SigningKeys"] = ValidSigningKeysJson(rsa),
            ["Security:Mfa:EncryptionKeys"] = ValidMfaKeysJson()
        };

        // 1. Valid production configuration passes
        var validConfig = new ConfigurationBuilder().AddInMemoryCollection(validBase).Build();
        Should.NotThrow(() => validConfig.ValidateStartupSecrets(env, requireHttpSecuritySecrets: true));

        // 2. Rejects postgrespassword
        var devPassword = new Dictionary<string, string?>(validBase)
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=loans;Username=postgres;Password=postgrespassword"
        };
        Should.Throw<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddInMemoryCollection(devPassword).Build().ValidateStartupSecrets(env));

        // 3. Rejects legacy Jwt:Key
        var withLegacyJwtKey = new Dictionary<string, string?>(validBase)
        {
            ["Jwt:Key"] = "legacy-secret-key"
        };
        Should.Throw<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddInMemoryCollection(withLegacyJwtKey).Build().ValidateStartupSecrets(env));

        // 4. Rejects missing Jwt:SigningKeys for WebApi
        var missingSigningKeys = new Dictionary<string, string?>(validBase);
        missingSigningKeys.Remove("Jwt:SigningKeys");
        Should.Throw<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddInMemoryCollection(missingSigningKeys).Build().ValidateStartupSecrets(env, requireHttpSecuritySecrets: true));

        // 5. Rejects missing Security:Mfa:EncryptionKeys for WebApi
        var missingMfaKeys = new Dictionary<string, string?>(validBase);
        missingMfaKeys.Remove("Security:Mfa:EncryptionKeys");
        Should.Throw<InvalidOperationException>(() =>
            new ConfigurationBuilder().AddInMemoryCollection(missingMfaKeys).Build().ValidateStartupSecrets(env, requireHttpSecuritySecrets: true));
    }

    [Fact]
    public void ValidateStartupSecrets_WorkersWithoutJwtOrMfaKeys_PassesWhenDatabaseIsValid_AndDevelopmentAllowsEmpty()
    {
        var prodEnv = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var workersConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=prod-db;Database=loans;Username=worker;Password=StrongWorkerPassword!99"
            })
            .Build();

        // Rule R8: Workers does NOT require Jwt:SigningKeys or Security:Mfa:EncryptionKeys
        Should.NotThrow(() => workersConfig.ValidateStartupSecrets(prodEnv, requireHttpSecuritySecrets: false));

        var devEnv = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var emptyDevConfig = new ConfigurationBuilder().Build();
        Should.NotThrow(() => emptyDevConfig.ValidateStartupSecrets(devEnv, requireHttpSecuritySecrets: true));
    }

    [Fact]
    public async Task AddSecretManager_KeyPerFile_LoadsSecretsAndReloadsOnFileRotation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loansystem-secrets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var rsa1 = RSA.Create(2048);
            using var rsa2 = RSA.Create(2048);

            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "ConnectionStrings__Database"),
                "Host=prod-db;Database=loans;Username=app;Password=SecretDbPassword!1");
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "Security__Mfa__EncryptionKeys"),
                ValidMfaKeysJson());
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "Jwt__SigningKeys"),
                ValidSigningKeysJson(rsa1));

            var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
            var builder = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Secrets:Provider"] = "KeyPerFile",
                    ["Secrets:Path"] = tempDir,
                    ["Secrets:ReloadInterval"] = "00:00:00.200"
                });

            builder.AddSecretManager(env);
            var configRoot = builder.Build();
            try
            {
                configRoot["ConnectionStrings:Database"].ShouldBe("Host=prod-db;Database=loans;Username=app;Password=SecretDbPassword!1");
                configRoot.ValidateStartupSecrets(env, requireHttpSecuritySecrets: true);

                using var keyRing = new JwtKeyRing(configRoot, new FixedClock(), env);
                var kid1 = keyRing.GetSigningCredentials().Kid;

                // Rotate secret file on disk with a newer active RSA key
                var json2 = JsonSerializer.Serialize(new
                {
                    keys = new[]
                    {
                        new { privateKeyPem = rsa1.ExportPkcs8PrivateKeyPem(), notBefore = DateTimeOffset.UtcNow.AddDays(-5), notAfter = DateTimeOffset.UtcNow.AddDays(30) },
                        new { privateKeyPem = rsa2.ExportPkcs8PrivateKeyPem(), notBefore = DateTimeOffset.UtcNow.AddMinutes(-1), notAfter = DateTimeOffset.UtcNow.AddDays(104) }
                    }
                });

                await File.WriteAllTextAsync(Path.Combine(tempDir, "Jwt__SigningKeys"), json2);

                for (var attempt = 0; attempt < 40 && keyRing.GetSigningCredentials().Kid == kid1; attempt++)
                {
                    await Task.Delay(100);
                }

                keyRing.GetSigningCredentials().Kid.ShouldNotBe(kid1);
                keyRing.GetValidationKeys().Count.ShouldBe(2);
            }
            finally
            {
                (configRoot as IDisposable)?.Dispose();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AddSecretManager_KeyPerFile_MissingDirectoryInProduction_ThrowsDirectoryNotFoundException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-secrets-{Guid.NewGuid():N}");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };

        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Provider"] = "KeyPerFile",
                ["Secrets:Path"] = missingPath
            });

        Should.Throw<DirectoryNotFoundException>(() => builder.AddSecretManager(env));
    }
}
