using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LoanSystem.Common.Adapters.Http.Idempotency;

/// <summary>
/// Endpoint filter enforcing presence of a non-empty <c>Idempotency-Key</c> header (BE-CUS-01/09/10).
/// Works alongside <see cref="IdempotencyMiddleware"/>, which stores and replays responses when the header is present.
/// </summary>
public sealed class RequireIdempotencyKeyFilter : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 128;

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;
        if (!headers.TryGetValue(HeaderName, out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()) ||
            values.ToString().Length > MaxKeyLength)
        {
            var problem = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Common.MissingIdempotencyKey",
                detail: $"Header '{HeaderName}' is required and must be at most {MaxKeyLength} characters.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "Common.MissingIdempotencyKey",
                    ["errorType"] = "Validation"
                });
            return ValueTask.FromResult<object?>(problem);
        }

        return next(context);
    }
}

public static class RequireIdempotencyKeyEndpointExtensions
{
    /// <summary>Requires a valid <c>Idempotency-Key</c> HTTP header on this endpoint.</summary>
    public static RouteHandlerBuilder RequireIdempotencyKey(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<RequireIdempotencyKeyFilter>();
}
