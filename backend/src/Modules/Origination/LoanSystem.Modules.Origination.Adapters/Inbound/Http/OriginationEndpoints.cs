using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Origination.Adapters.Inbound.Http;

public static class OriginationEndpoints
{
    public static IEndpointRouteBuilder MapOriginationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/origination")
            .WithTags("Origination");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Origination", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Analyst);

        return app;
    }
}
