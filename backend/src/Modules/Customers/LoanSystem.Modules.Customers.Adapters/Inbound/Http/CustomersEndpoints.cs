using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Http;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Customers", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Analyst);

        return app;
    }
}
