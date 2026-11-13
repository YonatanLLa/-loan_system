using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class JwtTokenValidationStatusTests
{
    private sealed class FakeUserStatusService : IUserStatusService
    {
        public bool IsActive { get; set; } = true;
        public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(IsActive);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "LoanSystem.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ReloadableConfigSource : ConfigurationProvider, IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

        public void SetSigningKeys(string json)
        {
            Data["Jwt:SigningKeys"] = json;
            OnReload();
        }
    }

    private sealed class WallClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTime LimaNow => DateTime.UtcNow.AddHours(-5);
        public DateOnly LimaToday => DateOnly.FromDateTime(LimaNow);
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

    private static (RequestDelegate Pipeline, ServiceProvider ServiceProvider, FakeUserStatusService StatusService, ReloadableConfigSource ConfigSource)
        CreateTestPipeline(RSA primaryKey, RSA? secondaryKey = null, int clockSkewSeconds = 60)
    {
        var now = DateTimeOffset.UtcNow;
        var keys = new List<(RSA, DateTimeOffset, DateTimeOffset)>
        {
            (primaryKey, now.AddMinutes(-10), now.AddDays(30))
        };
        if (secondaryKey is not null)
        {
            keys.Add((secondaryKey, now.AddMinutes(-5), now.AddDays(60)));
        }

        var configSource = new ReloadableConfigSource();
        configSource.SetSigningKeys(BuildSigningKeysJson(keys.ToArray()));

        var config = new ConfigurationBuilder()
            .Add(configSource)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "LoanSystem.Identity",
                ["Jwt:Audience"] = "LoanSystem.App",
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:ClockSkewSeconds"] = clockSkewSeconds.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IClock, WallClock>();
        services.AddLogging();
        services.AddRouting();

        var statusService = new FakeUserStatusService();
        services.AddSingleton<IUserStatusService>(statusService);

        services.AddSecurityAuthentication(config, new TestHostEnvironment());

        // Match SecurityModule post-configuration for live user status checks
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var existingOnTokenValidated = options.Events.OnTokenValidated;

            options.Events.OnTokenValidated = async context =>
            {
                if (existingOnTokenValidated is not null)
                {
                    await existingOnTokenValidated(context);
                    if (context.Result?.Failure is not null)
                    {
                        return;
                    }
                }

                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst("sub")?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    var status = context.HttpContext.RequestServices.GetRequiredService<IUserStatusService>();
                    var roleClaim = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                    var isValid = await status.IsUserSessionValidAsync(userId, roleClaim, context.HttpContext.RequestAborted);
                    if (!isValid)
                    {
                        context.Fail("The user account is suspended or inactive.");
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = null;
        });

        var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/api/protected", () => Results.Ok(new { status = "granted" }))
                .RequireAuthorization();
        });

        return (app.Build(), sp, statusService, configSource);
    }

    private static string CreateRsaToken(
        RSA key,
        string? kid,
        Guid userId,
        DateTime? notBeforeUtc = null,
        DateTime? expiresUtc = null)
    {
        var securityKey = new RsaSecurityKey(key) { KeyId = kid };
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "LoanSystem.Identity",
            audience: "LoanSystem.App",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, "ROLE_ANALYST")
            ],
            notBefore: notBeforeUtc ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string ComputeKid(RSA rsa)
    {
        var p = rsa.ExportParameters(false);
        var e = Base64UrlEncoder.Encode(p.Exponent!);
        var n = Base64UrlEncoder.Encode(p.Modulus!);
        var canonical = $"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static async Task<int> SendBearerRequestAsync(RequestDelegate pipeline, IServiceProvider sp, string token)
    {
        var context = new DefaultHttpContext { RequestServices = sp };
        context.Request.Path = "/api/protected";
        context.Request.Method = "GET";
        context.Request.Headers.Authorization = $"Bearer {token}";

        await pipeline(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task Request_WithActiveUserRs256Token_Returns200Ok()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, statusService, _) = CreateTestPipeline(rsa);
        await using var providerScope = sp;

        statusService.IsActive = true;
        var token = CreateRsaToken(rsa, ComputeKid(rsa), Guid.NewGuid());

        (await SendBearerRequestAsync(pipeline, sp, token)).ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Request_WithInactiveOrSuspendedUserToken_Returns401Unauthorized()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, statusService, _) = CreateTestPipeline(rsa);
        await using var providerScope = sp;

        statusService.IsActive = false;
        var token = CreateRsaToken(rsa, ComputeKid(rsa), Guid.NewGuid());

        (await SendBearerRequestAsync(pipeline, sp, token)).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Request_AlgorithmConfusionHs256SignedWithPublicKey_Returns401Unauthorized()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, _, _) = CreateTestPipeline(rsa);
        await using var providerScope = sp;

        var spkiBytes = rsa.ExportSubjectPublicKeyInfo();
        var hmacCreds = new SigningCredentials(
            new SymmetricSecurityKey(spkiBytes) { KeyId = ComputeKid(rsa) },
            SecurityAlgorithms.HmacSha256);

        var forgedToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "LoanSystem.Identity",
            audience: "LoanSystem.App",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "ROLE_ADMIN")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: hmacCreds));

        (await SendBearerRequestAsync(pipeline, sp, forgedToken)).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Request_UnsignedAlgNoneToken_Returns401Unauthorized()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, _, _) = CreateTestPipeline(rsa);
        await using var providerScope = sp;

        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            sub = Guid.NewGuid().ToString(),
            iss = "LoanSystem.Identity",
            aud = "LoanSystem.App",
            exp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()
        }));

        var unsignedToken = $"{header}.{payload}.";
        (await SendBearerRequestAsync(pipeline, sp, unsignedToken)).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Request_MissingOrUnknownKidHeader_Returns401Unauthorized()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, _, _) = CreateTestPipeline(rsa);
        await using var providerScope = sp;

        var missingKidToken = CreateRsaToken(rsa, kid: null, Guid.NewGuid());
        var unknownKidToken = CreateRsaToken(rsa, kid: "non-existent-kid", Guid.NewGuid());

        (await SendBearerRequestAsync(pipeline, sp, missingKidToken)).ShouldBe(StatusCodes.Status401Unauthorized);
        (await SendBearerRequestAsync(pipeline, sp, unknownKidToken)).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Request_KeyRevokedAfterReload_Returns401Unauthorized_WhileRetainedKeySucceeds()
    {
        using var key1 = RSA.Create(2048);
        using var key2 = RSA.Create(2048);
        var (pipeline, sp, _, configSource) = CreateTestPipeline(key1, key2);
        await using var providerScope = sp;

        var tokenFromKey1 = CreateRsaToken(key1, ComputeKid(key1), Guid.NewGuid());
        var tokenFromKey2 = CreateRsaToken(key2, ComputeKid(key2), Guid.NewGuid());

        (await SendBearerRequestAsync(pipeline, sp, tokenFromKey1)).ShouldBe(StatusCodes.Status200OK);
        (await SendBearerRequestAsync(pipeline, sp, tokenFromKey2)).ShouldBe(StatusCodes.Status200OK);

        // Revoke key1 by reloading configuration with only key2
        var now = DateTimeOffset.UtcNow;
        configSource.SetSigningKeys(BuildSigningKeysJson((key2, now.AddMinutes(-5), now.AddDays(60))));

        (await SendBearerRequestAsync(pipeline, sp, tokenFromKey1)).ShouldBe(StatusCodes.Status401Unauthorized);
        (await SendBearerRequestAsync(pipeline, sp, tokenFromKey2)).ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Request_ExpiredWithinClockSkew_Returns200_AndPastClockSkew_Returns401()
    {
        using var rsa = RSA.Create(2048);
        var (pipeline, sp, _, _) = CreateTestPipeline(rsa, clockSkewSeconds: 60);
        await using var providerScope = sp;

        var kid = ComputeKid(rsa);
        var withinSkewToken = CreateRsaToken(
            rsa,
            kid,
            Guid.NewGuid(),
            notBeforeUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddSeconds(-30));

        var pastSkewToken = CreateRsaToken(
            rsa,
            kid,
            Guid.NewGuid(),
            notBeforeUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddSeconds(-90));

        (await SendBearerRequestAsync(pipeline, sp, withinSkewToken)).ShouldBe(StatusCodes.Status200OK);
        (await SendBearerRequestAsync(pipeline, sp, pastSkewToken)).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task UserStatusService_InvalidateAndActivate_TogglesCacheDirectly()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserStatusService(memoryCache, null!);
        var userId = Guid.NewGuid();

        service.InvalidateUser(userId);
        (await service.IsUserActiveAsync(userId)).ShouldBeFalse();

        service.ActivateUser(userId);
        (await service.IsUserActiveAsync(userId)).ShouldBeTrue();
    }
}
