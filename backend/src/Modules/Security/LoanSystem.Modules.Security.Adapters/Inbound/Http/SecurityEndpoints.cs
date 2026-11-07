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

        return app;
    }
}
