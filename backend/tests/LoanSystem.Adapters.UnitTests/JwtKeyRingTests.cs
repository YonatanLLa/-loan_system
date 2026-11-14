using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class JwtKeyRingTests
{
    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        public DateTime LimaNow => UtcNow.UtcDateTime.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ReloadableSigningKeysSource : ConfigurationProvider, IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

        public void SetValue(string key, string? value)
        {
            if (value is null)
            {
                Data.Remove(key);
            }
            else
            {
                Data[key] = value;
            }

            OnReload();
        }
    }

    private static string BuildSigningKeysJson(params (RSA Key, DateTimeOffset NotBefore, DateTimeOffset NotAfter)[] keys)
    {
        var payload = new
        {
            keys = keys.Select(k => new
            {
                privateKeyPem = k.Key.ExportPkcs8PrivateKeyPem(),
                notBefore = k.NotBefore,
                notAfter = k.NotAfter
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    private static IConfiguration BuildConfiguration(
        string? signingKeysJson,
        Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>();
        if (signingKeysJson is not null)
        {
            values["Jwt:SigningKeys"] = signingKeysJson;
        }

        if (extra is not null)
        {
            foreach (var (k, v) in extra)
            {
                values[k] = v;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void GetSigningCredentials_WithMultipleActiveKeys_SelectsNewestNotBefore()
    {
        var clock = new MutableClock();
        using var older = RSA.Create(2048);
        using var newer = RSA.Create(2048);

        var json = BuildSigningKeysJson(
            (older, clock.UtcNow.AddDays(-10), clock.UtcNow.AddDays(30)),
            (newer, clock.UtcNow.AddHours(-1), clock.UtcNow.AddDays(90)));

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var sut = new JwtKeyRing(BuildConfiguration(json), clock, env);

        var creds = sut.GetSigningCredentials();
        var newerCredsJson = BuildSigningKeysJson((newer, clock.UtcNow.AddHours(-1), clock.UtcNow.AddDays(90)));
        using var newerOnly = new JwtKeyRing(BuildConfiguration(newerCredsJson), clock, env);

        creds.Kid.ShouldBe(newerOnly.GetSigningCredentials().Kid);
    }

    [Fact]
    public void ResolveValidationKeys_PrePublishedKey_IsNotActiveForSigning_ButPublishedAndValidForVerification()
    {
        var clock = new MutableClock();
        using var current = RSA.Create(2048);
        using var next = RSA.Create(2048);

        var json = BuildSigningKeysJson(
            (current, clock.UtcNow.AddDays(-5), clock.UtcNow.AddDays(25)),
            (next, clock.UtcNow.AddHours(24), clock.UtcNow.AddDays(120)));

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var sut = new JwtKeyRing(BuildConfiguration(json), clock, env);

        var validationKeys = sut.GetValidationKeys();
        validationKeys.Count.ShouldBe(2);

        var activeKid = sut.GetSigningCredentials().Kid;
        var nextKid = validationKeys.Single(k => k.Kid != activeKid).Kid;

        sut.ResolveValidationKeys(activeKid).Count.ShouldBe(1);
        sut.ResolveValidationKeys(nextKid).Count.ShouldBe(1);

        // Once clock crosses next.notBefore, signing automatically transitions to next key.
        clock.UtcNow = clock.UtcNow.AddHours(24);
        sut.GetSigningCredentials().Kid.ShouldBe(nextKid);
    }

    [Fact]
    public void ResolveValidationKeys_ExpiredKey_RetainedDuringTtlPlusClockSkew_ThenEvicted()
    {
        var clock = new MutableClock();
        using var expiring = RSA.Create(2048);
        using var replacement = RSA.Create(2048);

        var expiringNotAfter = clock.UtcNow.AddMinutes(5);
        var json = BuildSigningKeysJson(
            (expiring, clock.UtcNow.AddDays(-30), expiringNotAfter),
            (replacement, clock.UtcNow.AddMinutes(-1), clock.UtcNow.AddDays(90)));

        var config = BuildConfiguration(json, new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Jwt:ClockSkewSeconds"] = "60"
        });

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var sut = new JwtKeyRing(config, clock, env);

        var activeKid = sut.GetSigningCredentials().Kid;
        var expiringKid = sut.GetValidationKeys().Single(k => k.Kid != activeKid).Kid;

        // 59 seconds before retention cutoff (notAfter + 30m + 60s): still accepted
        clock.UtcNow = expiringNotAfter.AddMinutes(30).AddSeconds(59);
        sut.ResolveValidationKeys(expiringKid).Count.ShouldBe(1);
        sut.GetValidationKeys().Any(k => k.Kid == expiringKid).ShouldBeTrue();

        // 1 second past retention cutoff: evicted
        clock.UtcNow = expiringNotAfter.AddMinutes(30).AddSeconds(61);
        sut.ResolveValidationKeys(expiringKid).Count.ShouldBe(0);
        sut.GetValidationKeys().Any(k => k.Kid == expiringKid).ShouldBeFalse();
    }

    [Fact]
    public void EnsureInitialized_WeakOrMalformedOrDuplicateKeys_ThrowsInvalidOperationException()
    {
        var clock = new MutableClock();
        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };

        // < 2048 bits rejected
        using var weak = RSA.Create(1024);
        var weakJson = BuildSigningKeysJson((weak, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddDays(30)));
        using (var sutWeak = new JwtKeyRing(BuildConfiguration(weakJson), clock, env))
        {
            Should.Throw<InvalidOperationException>(() => sutWeak.EnsureInitialized());
        }

        // Duplicate kid rejected
        using var dup = RSA.Create(2048);
        var dupJson = BuildSigningKeysJson(
            (dup, clock.UtcNow.AddMinutes(-10), clock.UtcNow.AddDays(30)),
            (dup, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddDays(60)));
        using (var sutDup = new JwtKeyRing(BuildConfiguration(dupJson), clock, env))
        {
            Should.Throw<InvalidOperationException>(() => sutDup.EnsureInitialized());
        }

        // notAfter <= notBefore rejected
        var badWindowJson = BuildSigningKeysJson((dup, clock.UtcNow.AddDays(5), clock.UtcNow.AddDays(5)));
        using (var sutWindow = new JwtKeyRing(BuildConfiguration(badWindowJson), clock, env))
        {
            Should.Throw<InvalidOperationException>(() => sutWindow.EnsureInitialized());
        }

        // Public key PEM instead of PKCS#8 private key rejected
        var publicOnlyJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    privateKeyPem = dup.ExportSubjectPublicKeyInfoPem(),
                    notBefore = clock.UtcNow.AddMinutes(-5),
                    notAfter = clock.UtcNow.AddDays(30)
                }
            }
        });
        using (var sutPub = new JwtKeyRing(BuildConfiguration(publicOnlyJson), clock, env))
        {
            Should.Throw<InvalidOperationException>(() => sutPub.EnsureInitialized());
        }
    }

    [Fact]
    public async Task Reload_WithInvalidJson_KeepsPreviousSnapshotAndReportsDegradedHealth()
    {
        var clock = new MutableClock();
        using var initial = RSA.Create(2048);
        var initialJson = BuildSigningKeysJson((initial, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddDays(40)));

        var source = new ReloadableSigningKeysSource();
        source.SetValue("Jwt:SigningKeys", initialJson);

        var config = new ConfigurationBuilder()
            .Add(source)
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var sut = new JwtKeyRing(config, clock, env);
        sut.EnsureInitialized();

        var initialKid = sut.GetSigningCredentials().Kid;
        sut.GetStatus().State.ShouldBe(JwtHealthState.Healthy);

        // Push corrupted reload
        source.SetValue("Jwt:SigningKeys", "{ invalid-json");

        sut.GetSigningCredentials().Kid.ShouldBe(initialKid);
        sut.ResolveValidationKeys(initialKid).Count.ShouldBe(1);
        sut.GetStatus().State.ShouldBe(JwtHealthState.Degraded);

        var healthCheck = new JwtKeyRingHealthCheck(sut);
        var hcResult = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        hcResult.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void MissingSigningKeys_ThrowsInProduction_GeneratesStableEphemeralKeyInDevelopment()
    {
        var clock = new MutableClock();

        var prodEnv = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var prodSut = new JwtKeyRing(BuildConfiguration(null), clock, prodEnv);
        Should.Throw<InvalidOperationException>(() => prodSut.EnsureInitialized());

        var source = new ReloadableSigningKeysSource();
        var devConfig = new ConfigurationBuilder().Add(source).Build();
        var devEnv = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var devSut = new JwtKeyRing(devConfig, clock, devEnv);
        var creds1 = devSut.GetSigningCredentials();
        creds1.Kid.ShouldNotBeNullOrWhiteSpace();

        // Unrelated configuration reload keeps the same ephemeral dev key
        source.SetValue("Some:OtherSetting", "changed");
        var creds2 = devSut.GetSigningCredentials();
        creds2.Kid.ShouldBe(creds1.Kid);
    }

    [Fact]
    public void GetStatus_ActiveKeyNearExpiry_ReportsDegraded_AndAllExpired_ReportsUnhealthy()
    {
        var clock = new MutableClock();
        using var key = RSA.Create(2048);
        var json = BuildSigningKeysJson((key, clock.UtcNow.AddDays(-90), clock.UtcNow.AddDays(10)));

        var config = BuildConfiguration(json, new Dictionary<string, string?>
        {
            ["Jwt:RotationWarningDays"] = "14"
        });

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var sut = new JwtKeyRing(config, clock, env);
        sut.EnsureInitialized();

        sut.GetStatus().State.ShouldBe(JwtHealthState.Degraded);

        clock.UtcNow = clock.UtcNow.AddDays(11);
        sut.GetStatus().State.ShouldBe(JwtHealthState.Unhealthy);
    }
}
