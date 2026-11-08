using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Loans.Adapters.Inbound.Http;

public static class LoansEndpoints
{
    public static IEndpointRouteBuilder MapLoansEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loans")
            .WithTags("Loans");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Loans", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Analyst);

        return app;
    }
}
