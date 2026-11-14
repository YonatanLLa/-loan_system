using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Security.Adapters;
using LoanSystem.Modules.Security.Adapters.Inbound.Http;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class JwtTokenGeneratorAndJwksTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; init; } = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
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

    [Fact]
    public void Generate_SignsWithRs256_DeterministicRfc7638Kid_AndExpectedClaims()
    {
        var clock = new FixedClock();
        using var rsa = RSA.Create(2048);

        var pubParams = rsa.ExportParameters(includePrivateParameters: false);
        var e = Base64UrlEncoder.Encode(pubParams.Exponent!);
        var n = Base64UrlEncoder.Encode(pubParams.Modulus!);
        var expectedKid = Base64UrlEncoder.Encode(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}")));

        var signingKeysJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    privateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
                    notBefore = clock.UtcNow.AddMinutes(-5),
                    notAfter = clock.UtcNow.AddDays(30)
                }
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKeys"] = signingKeysJson,
                ["Jwt:Issuer"] = "LoanSystem.Identity",
                ["Jwt:Audience"] = "LoanSystem.App",
                ["Jwt:AccessTokenMinutes"] = "30"
            })
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        using var keyRing = new JwtKeyRing(config, clock, env);
        var generator = new JwtTokenGenerator(keyRing, config, clock);

        var user = User.Reconstitute(
            Guid.NewGuid(),
            "analyst@loansystem.pe",
            "hash",
            "Ana Analyst",
            SecurityRole.ROLE_ANALYST,
            UserStatus.ACTIVE);

        var (accessToken, expiresAt) = generator.Generate(user);
        expiresAt.ShouldBe(clock.UtcNow.AddMinutes(30));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);

        jwt.Header.Alg.ShouldBe(SecurityAlgorithms.RsaSha256);
        jwt.Header.Kid.ShouldBe(expectedKid);
        jwt.Issuer.ShouldBe("LoanSystem.Identity");
        jwt.Audiences.ShouldContain("LoanSystem.App");
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.ShouldBe(user.Id.ToString());
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.ShouldBe(user.Email);
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Name).Value.ShouldBe(user.FullName);
        jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value.ShouldBe(SecurityRole.ROLE_ANALYST.ToString());
        jwt.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.Claims.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Iat);

        // Cryptographic signature must validate using ONLY the public parameters
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "LoanSystem.Identity",
            ValidateAudience = true,
            ValidAudience = "LoanSystem.App",
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKey = new RsaSecurityKey(pubParams) { KeyId = expectedKid }
        };

        handler.ValidateToken(accessToken, validationParams, out _);
    }

    [Fact]
    public async Task JwksEndpoint_IsAnonymous_Caches300s_PublishesPrePublishedKeys_AndNeverExposesPrivateFields()
    {
        var now = DateTimeOffset.UtcNow;
        using var active = RSA.Create(2048);
        using var prePublished = RSA.Create(2048);

        var signingKeysJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { privateKeyPem = active.ExportPkcs8PrivateKeyPem(), notBefore = now.AddMinutes(-5), notAfter = now.AddDays(30) },
                new { privateKeyPem = prePublished.ExportPkcs8PrivateKeyPem(), notBefore = now.AddHours(24), notAfter = now.AddDays(120) }
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=localhost;Database=test;Username=u;Password=secret",
                ["Jwt:SigningKeys"] = signingKeysJson,
                ["Security:Mfa:EncryptionKeys"] = "{\"active\":\"k1\",\"keys\":{\"k1\":\"" +
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "\"}}"
            })
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var services = new ServiceCollection();
        var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddRouting();
        services.AddHttpContextAccessor();
        services.AddSecurityAuthentication(config, env);
        services.AddAuthorization();

        await using var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapSecurityEndpoints());

        var pipeline = app.Build();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp,
            Response = { Body = new MemoryStream() }
        };
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/.well-known/jwks.json";

        await pipeline(httpContext);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        httpContext.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=300");

        httpContext.Response.Body.Position = 0;
        using var bodyReader = new StreamReader(httpContext.Response.Body);
        var json = await bodyReader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var keysElement = doc.RootElement.GetProperty("keys");
        keysElement.GetArrayLength().ShouldBe(2);

        foreach (var jwk in keysElement.EnumerateArray())
        {
            jwk.GetProperty("kty").GetString().ShouldBe("RSA");
            jwk.GetProperty("use").GetString().ShouldBe("sig");
            jwk.GetProperty("alg").GetString().ShouldBe("RS256");
            jwk.GetProperty("kid").GetString().ShouldNotBeNullOrWhiteSpace();
            jwk.GetProperty("n").GetString().ShouldNotBeNullOrWhiteSpace();
            jwk.GetProperty("e").GetString().ShouldNotBeNullOrWhiteSpace();

            // RFC 7517 private RSA parameters must NEVER be present
            jwk.TryGetProperty("d", out _).ShouldBeFalse();
            jwk.TryGetProperty("p", out _).ShouldBeFalse();
            jwk.TryGetProperty("q", out _).ShouldBeFalse();
            jwk.TryGetProperty("dp", out _).ShouldBeFalse();
            jwk.TryGetProperty("dq", out _).ShouldBeFalse();
            jwk.TryGetProperty("qi", out _).ShouldBeFalse();
        }
    }
}
