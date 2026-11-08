using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class PolicyAuthorizationTests
{
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuthScheme";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Context.User.Identity?.IsAuthenticated == true)
            {
                var ticket = new AuthenticationTicket(Context.User, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }

    private readonly ServiceProvider _serviceProvider;
    private readonly RequestDelegate _pipeline;
    private readonly Dictionary<string, int> _executionCounters = new()
    {
        ["admin"] = 0,
        ["operator"] = 0,
        ["analyst"] = 0,
        ["collector"] = 0,
        ["auditor"] = 0,
        ["client"] = 0,
        ["fallback"] = 0,
        ["anonymous"] = 0
    };

    public PolicyAuthorizationTests()
    {
        var services = new ServiceCollection();
        var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddLogging();
        services.AddRouting();

        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SecurityPolicies.Admin, p => p.RequireRole(SecurityPolicies.Admin));
            options.AddPolicy(SecurityPolicies.Analyst, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Analyst));
            options.AddPolicy(SecurityPolicies.Operator, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Operator));
            options.AddPolicy(SecurityPolicies.Collector, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Collector));
            options.AddPolicy(SecurityPolicies.Auditor, p => p.RequireRole(SecurityPolicies.Admin, SecurityPolicies.Auditor));
            options.AddPolicy(SecurityPolicies.Client, p => p.RequireRole(SecurityPolicies.Client));

            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        _serviceProvider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(_serviceProvider);
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/test/admin", () =>
            {
                _executionCounters["admin"]++;
                return Results.Ok(new { message = "admin ok" });
            }).RequireAuthorization(SecurityPolicies.Admin);

            endpoints.MapGet("/test/operator", () =>
            {
                _executionCounters["operator"]++;
                return Results.Ok(new { message = "operator ok" });
            }).RequireAuthorization(SecurityPolicies.Operator);

            endpoints.MapGet("/test/analyst", () =>
            {
                _executionCounters["analyst"]++;
                return Results.Ok(new { message = "analyst ok" });
            }).RequireAuthorization(SecurityPolicies.Analyst);

            endpoints.MapGet("/test/collector", () =>
            {
                _executionCounters["collector"]++;
                return Results.Ok(new { message = "collector ok" });
            }).RequireAuthorization(SecurityPolicies.Collector);

            endpoints.MapGet("/test/auditor", () =>
            {
                _executionCounters["auditor"]++;
                return Results.Ok(new { message = "auditor ok" });
            }).RequireAuthorization(SecurityPolicies.Auditor);

            endpoints.MapGet("/test/client", () =>
            {
                _executionCounters["client"]++;
                return Results.Ok(new { message = "client ok" });
            }).RequireAuthorization(SecurityPolicies.Client);

            endpoints.MapGet("/test/fallback", () =>
            {
                _executionCounters["fallback"]++;
                return Results.Ok(new { message = "fallback ok" });
            });

            endpoints.MapGet("/test/anonymous", () =>
            {
                _executionCounters["anonymous"]++;
                return Results.Ok(new { message = "anonymous ok" });
            }).AllowAnonymous();
        });

        _pipeline = app.Build();
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id")
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private async Task<HttpContext> ExecuteRequestAsync(string path, ClaimsPrincipal? principal = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = _serviceProvider
        };
        context.Request.Path = path;
        context.Request.Method = "GET";

        if (principal != null)
        {
            context.User = principal;
        }

        await _pipeline(context);
        return context;
    }

    [Fact]
    public async Task InsufficientRole_Returns403Forbidden_AndDoesNotExecuteEndpointHandler()
    {
        // ROLE_OPERATOR trying to access ROLE_ADMIN policy endpoint
        var operatorUser = CreatePrincipal(SecurityPolicies.Operator);

        var context = await ExecuteRequestAsync("/test/admin", operatorUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _executionCounters["admin"].ShouldBe(0); // Handler was NEVER executed ("403 sin ejecutar nada")
    }

    [Fact]
    public async Task ValidRole_Returns200Ok_AndExecutesEndpointHandler()
    {
        var operatorUser = CreatePrincipal(SecurityPolicies.Operator);

        var context = await ExecuteRequestAsync("/test/operator", operatorUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters["operator"].ShouldBe(1);
    }

    [Fact]
    public async Task AdminRole_AccessesAdminEndpoint_Returns200Ok()
    {
        var adminUser = CreatePrincipal(SecurityPolicies.Admin);

        var context = await ExecuteRequestAsync("/test/admin", adminUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters["admin"].ShouldBe(1);
    }

    [Fact]
    public async Task UnauthenticatedUser_Returns401Unauthorized_AndDoesNotExecuteEndpointHandler()
    {
        var context = await ExecuteRequestAsync("/test/admin", principal: null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        _executionCounters["admin"].ShouldBe(0);
    }

    [Theory]
    [InlineData("/test/operator", "operator")]
    [InlineData("/test/analyst", "analyst")]
    [InlineData("/test/collector", "collector")]
    [InlineData("/test/auditor", "auditor")]
    public async Task AdminHierarchy_CanAccessSubordinatePolicies(string endpoint, string counterKey)
    {
        // ROLE_ADMIN can access Analyst, Operator, Collector, and Auditor policies
        var adminUser = CreatePrincipal(SecurityPolicies.Admin);

        var context = await ExecuteRequestAsync(endpoint, adminUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters[counterKey].ShouldBe(1);
    }

    [Fact]
    public async Task Admin_CannotAccessClientPolicyEndpoint()
    {
        // ROLE_CLIENT policy is strictly for client role, not admin
        var adminUser = CreatePrincipal(SecurityPolicies.Admin);

        var context = await ExecuteRequestAsync("/test/client", adminUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _executionCounters["client"].ShouldBe(0);
    }

    [Fact]
    public async Task ClientRole_CanAccessClientEndpoint()
    {
        var clientUser = CreatePrincipal(SecurityPolicies.Client);

        var context = await ExecuteRequestAsync("/test/client", clientUser);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters["client"].ShouldBe(1);
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAuthentication_WhenNoPolicySpecified()
    {
        // Unauthenticated request to endpoint without metadata should return 401
        var unauthenticatedContext = await ExecuteRequestAsync("/test/fallback", principal: null);
        unauthenticatedContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        _executionCounters["fallback"].ShouldBe(0);

        // Authenticated request to endpoint without metadata should succeed
        var authenticatedUser = CreatePrincipal("SOME_ROLE");
        var authenticatedContext = await ExecuteRequestAsync("/test/fallback", authenticatedUser);
        authenticatedContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters["fallback"].ShouldBe(1);
    }

    [Fact]
    public async Task AllowAnonymous_BypassesFallbackPolicy_AndExecutesHandler()
    {
        var context = await ExecuteRequestAsync("/test/anonymous", principal: null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        _executionCounters["anonymous"].ShouldBe(1);
    }

    [Theory]
    [InlineData(SecurityPolicies.Analyst, "/test/admin", "admin")]
    [InlineData(SecurityPolicies.Analyst, "/test/operator", "operator")]
    [InlineData(SecurityPolicies.Collector, "/test/admin", "admin")]
    [InlineData(SecurityPolicies.Auditor, "/test/admin", "admin")]
    [InlineData(SecurityPolicies.Client, "/test/admin", "admin")]
    public async Task DisallowedRoles_Return403Forbidden_AndHandlerCountRemainsZero(string role, string path, string counterKey)
    {
        var user = CreatePrincipal(role);

        var context = await ExecuteRequestAsync(path, user);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _executionCounters[counterKey].ShouldBe(0);
    }
}
