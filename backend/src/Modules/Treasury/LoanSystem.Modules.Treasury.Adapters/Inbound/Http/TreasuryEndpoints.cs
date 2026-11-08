using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Treasury.Adapters.Inbound.Http;

public static class TreasuryEndpoints
{
    public static IEndpointRouteBuilder MapTreasuryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/treasury")
            .WithTags("Treasury");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Treasury", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Operator);

        return app;
    }
}
