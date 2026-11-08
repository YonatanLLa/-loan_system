using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Products.Core.Contracts;
using LoanSystem.Modules.Products.Core.Ports.Inbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Products.Adapters.Inbound.Http;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Products", Status = "Ready" }))
            .RequireAuthorization(SecurityPolicies.Admin);

        group.MapPost("/simulate", async (SimulateScheduleRequest request, ISimulateScheduleUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(request, ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("SimulateSchedule")
        .WithSummary("Simulate a loan payment schedule (French method)");

        return app;
    }
}
