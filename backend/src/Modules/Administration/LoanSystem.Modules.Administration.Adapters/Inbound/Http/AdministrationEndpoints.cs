using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Administration.Adapters.Inbound.Http;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/administration")
            .WithTags("Administration");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Administration", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Admin);

        return app;
    }
}
