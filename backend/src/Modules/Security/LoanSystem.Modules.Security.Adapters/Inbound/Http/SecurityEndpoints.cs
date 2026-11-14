using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace LoanSystem.Modules.Security.Adapters.Inbound.Http;

public static class SecurityEndpoints
{
    private const string RefreshTokenReusedCode = "Security.RefreshTokenReused";
    private const string RefreshLoggerCategory = "LoanSystem.Modules.Security.RefreshToken";

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
        .WithSummary("Authenticate with email and password; returns tokens or an MFA challenge for privileged roles (BE-SEC-01, BE-SEC-03)");

        auth.MapPost("/mfa/verify", async (MfaVerifyRequest request, IVerifyMfaUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("VerifyMfa")
        .WithSummary("Verify a TOTP or recovery code for an active MFA challenge and issue tokens (BE-SEC-03)");

        auth.MapPost("/mfa/enroll", async (MfaEnrollRequest request, IBeginMfaEnrollmentUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("BeginMfaEnrollment")
        .WithSummary("Generate a TOTP secret and otpauth URI for mandatory first-login MFA enrollment (BE-SEC-03)");

        auth.MapPost("/mfa/enroll/confirm", async (MfaEnrollConfirmRequest request, IConfirmMfaEnrollmentUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("ConfirmMfaEnrollment")
        .WithSummary("Confirm MFA enrollment with a TOTP code, issue tokens, and return single-use recovery codes (BE-SEC-03)");

        auth.MapPost("/refresh", async (
            RefreshRequest request, IRefreshTokenUseCase useCase, HttpContext http, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            if (result.IsFailure && result.Error.Code == RefreshTokenReusedCode)
            {
                // Core has no logging dependency: the security signal is logged here (DECISION_02).
                loggerFactory.CreateLogger(RefreshLoggerCategory).LogWarning(
                    "Refresh token reuse detected: the whole token family was revoked. RemoteIp={RemoteIp} UserAgent={UserAgent}",
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString());
            }

            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Rotate a refresh token: revokes the one presented and issues a new token pair in the same family; reusing an already rotated token revokes the whole family (BE-SEC-01)");

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

        app.MapPost("/api/users/{id:guid}/mfa/reset", async (Guid id, IResetUserMfaUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new ResetUserMfaRequest(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Admin)
        .WithTags("Security")
        .WithName("ResetUserMfa")
        .WithSummary("Reset a user's MFA configuration and revoke their active sessions (BE-SEC-03)");

        app.MapPut("/api/users/{id:guid}/role", async (
            Guid id,
            ChangeUserRoleRequest body,
            IRequestUserRoleChangeUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(new RequestUserRoleChangeCommand(id, body.NewRole, body.Reason), ct);
            return result.IsSuccess
                ? Results.Accepted($"/api/approvals/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Admin)
        .WithTags("Security")
        .WithName("RequestUserRoleChange")
        .WithSummary("Submit a four-eyes approval request to change a user's RBAC role (BE-SEC-09)");

        app.MapGet("/api/audit-logs", async ([AsParameters] AuditLogQuery query, IQueryAuditLogsUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityPolicies.Auditor)
        .WithTags("Security")
        .WithName("QueryAuditLogs")
        .WithSummary("Query immutable audit log entries with filters and pagination (BE-SEC-08)");

        app.MapGet("/.well-known/jwks.json", async (
            HttpContext httpContext,
            IGetJwksUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(ct);
            if (result.IsSuccess)
            {
                httpContext.Response.Headers.CacheControl = "public, max-age=300";
            }

            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithTags("Security")
        .WithName("GetJwks")
        .WithSummary("Publish active and retained RS256 public signing keys (JWKS) for JWT verification (BE-SEC-10)");

        app.MapApprovalsEndpoints();

        return app;
    }
}
