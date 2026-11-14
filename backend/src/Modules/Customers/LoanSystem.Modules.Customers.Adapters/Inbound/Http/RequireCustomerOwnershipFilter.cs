using System.Net;
using LoanSystem.Common.Adapters.Http;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Ports.Outbound;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.Modules.Customers.Adapters.Inbound.Http;

internal static class HttpEndpointExtensions
{
    public static RouteHandlerBuilder RequireCustomerOwnershipOrStaff(
        this RouteHandlerBuilder builder,
        params string[] allowedStaffRoles)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
            foreach (var role in allowedStaffRoles)
            {
                if (currentUser.IsInRole(role))
                {
                    return await next(context);
                }
            }

            if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var rawId)
                && Guid.TryParse(rawId?.ToString(), out var routeCustomerId)
                && currentUser.IsInRole(SecurityPolicies.Client)
                && currentUser.CustomerId == routeCustomerId)
            {
                return await next(context);
            }

            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Customers.NotFound",
                detail: "Customer was not found.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "Customers.NotFound",
                    ["errorType"] = "NotFound"
                });
        });
    }

    public static IResult ToNoContentResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.ToHttpResult();

    public static bool TryParseIfMatchVersion(HttpRequest request, out int? version)
    {
        version = null;
        if (!request.Headers.TryGetValue("If-Match", out var values))
        {
            return true;
        }

        var raw = values.ToString().Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return true;
        }

        if (raw.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[2..].Trim();
        }

        raw = raw.Trim('"');
        if (int.TryParse(raw, out var parsed) && parsed > 0)
        {
            version = parsed;
            return true;
        }

        return false;
    }

    public static IResult ValidationProblem(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Customers.Validation",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "Customers.Validation",
                ["errorType"] = "Validation"
            });
}
