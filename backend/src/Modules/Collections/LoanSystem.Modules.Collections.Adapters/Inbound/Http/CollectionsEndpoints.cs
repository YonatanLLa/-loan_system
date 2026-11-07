using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Modules.Collections.Adapters.Inbound.Http;

public static class CollectionsEndpoints
{
    public static IEndpointRouteBuilder MapCollectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/collections")
            .WithTags("Collections");

        group.MapGet("/ping", () => Results.Ok(new { Module = "Collections", Status = "Ready" }));

        return app;
    }
}
