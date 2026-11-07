using LoanSystem.Common.Adapters.Http;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Security.Adapters.Inbound.Http;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security")
            .WithTags("Security");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Security", Status = "Ready" }));

        var auth = app.MapGroup("/api/auth")
            .WithTags("Security");

        auth.MapPost("/login", async (LoginRequest request, ILoginUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate with email and password; returns a short-lived JWT and a rotating refresh token (BE-SEC-01)");

        auth.MapPost("/refresh", async (RefreshRequest request, IRefreshTokenUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Rotate a refresh token: revokes the one presented and issues a new token pair (BE-SEC-01)");

        return app;
    }
}
