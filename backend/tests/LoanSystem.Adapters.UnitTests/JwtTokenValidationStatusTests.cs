using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoanSystem.Modules.Security.Adapters.Outbound.Providers;
using LoanSystem.Modules.Security.Core.Ports.Outbound;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class JwtTokenValidationStatusTests
{
    private const string SecretKey = "secret-key-with-at-least-32-bytes-length-123456";

    private sealed class FakeUserStatusService : IUserStatusService
    {
        public bool IsActive { get; set; } = true;
        public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(IsActive);
    }

    private static string GenerateJwt(Guid userId, SymmetricSecurityKey key)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "ROLE_ANALYST")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static (RequestDelegate Pipeline, IServiceProvider ServiceProvider, FakeUserStatusService StatusService, SymmetricSecurityKey SigningKey)
        CreateTestPipeline()
    {
        var services = new ServiceCollection();
        var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddLogging();
        services.AddRouting();

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var statusService = new FakeUserStatusService();
        services.AddSingleton<IUserStatusService>(statusService);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey
                };
            });

        // Register the post-configuration as done in SecurityModule
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
                    var isActive = await status.IsUserActiveAsync(userId, context.HttpContext.RequestAborted);
                    if (!isActive)
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

        return (app.Build(), sp, statusService, signingKey);
    }

    [Fact]
    public async Task Request_WithActiveUserToken_Returns200Ok()
    {
        var (pipeline, sp, statusService, signingKey) = CreateTestPipeline();
        var userId = Guid.NewGuid();
        statusService.IsActive = true;
        var token = GenerateJwt(userId, signingKey);

        var context = new DefaultHttpContext { RequestServices = sp };
        context.Request.Path = "/api/protected";
        context.Request.Method = "GET";
        context.Request.Headers.Authorization = $"Bearer {token}";

        await pipeline(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Request_WithInactiveOrSuspendedUserToken_Returns401Unauthorized()
    {
        var (pipeline, sp, statusService, signingKey) = CreateTestPipeline();
        var userId = Guid.NewGuid();
        statusService.IsActive = false; // User has been suspended
        var token = GenerateJwt(userId, signingKey);

        var context = new DefaultHttpContext { RequestServices = sp };
        context.Request.Path = "/api/protected";
        context.Request.Method = "GET";
        context.Request.Headers.Authorization = $"Bearer {token}";

        await pipeline(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task UserStatusService_InvalidateAndActivate_TogglesCacheDirectly()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserStatusService(memoryCache, null!);
        var userId = Guid.NewGuid();

        // When invalidated
        service.InvalidateUser(userId);
        var activeAfterInvalidate = await service.IsUserActiveAsync(userId);
        activeAfterInvalidate.ShouldBeFalse();

        // When activated
        service.ActivateUser(userId);
        var activeAfterActivate = await service.IsUserActiveAsync(userId);
        activeAfterActivate.ShouldBeTrue();
    }
}
