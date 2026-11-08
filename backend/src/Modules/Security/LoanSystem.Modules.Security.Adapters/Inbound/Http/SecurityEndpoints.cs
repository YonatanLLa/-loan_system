using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Application;
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

        group.MapGet("/ping", () => Results.Ok(new { Module = "Security", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Admin);

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

        auth.MapPost("/logout", async (LogoutRequest? request, ILogoutUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request ?? new LogoutRequest(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Revoke the refresh token or all active sessions of the authenticated user (BE-SEC-04)");

        app.MapGet("/api/me", async (IGetCurrentUserUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithTags("Security")
        .WithName("GetCurrentUser")
        .WithSummary("Return the currently authenticated user's profile, role and effective permissions (BE-SEC-04)");

        app.MapPost("/api/users/{id:guid}/suspend", async (Guid id, ISuspendUserUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new SuspendUserRequest(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Admin)
        .WithTags("Security")
        .WithName("SuspendUser")
        .WithSummary("Suspend a user and immediately invalidate all their active refresh tokens (BE-SEC-06)");

        app.MapPost("/api/users/{id:guid}/activate", async (Guid id, IActivateUserUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new ActivateUserRequest(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Admin)
        .WithTags("Security")
        .WithName("ActivateUser")
        .WithSummary("Reactivate a suspended user account (BE-SEC-06)");

        app.MapGet("/api/audit-logs", async ([AsParameters] AuditLogQuery query, IQueryAuditLogsUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Auditor)
        .WithTags("Security")
        .WithName("QueryAuditLogs")
        .WithSummary("Query immutable audit log entries with filters and pagination (BE-SEC-08)");

        return app;
    }
}
