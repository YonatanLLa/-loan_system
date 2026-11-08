using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Integrations.Adapters.Inbound.Http;

public static class IntegrationsEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations")
            .WithTags("Integrations");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Integrations", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Operator);

        return app;
    }
}
