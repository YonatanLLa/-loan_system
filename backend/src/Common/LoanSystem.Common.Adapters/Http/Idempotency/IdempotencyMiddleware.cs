using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoanSystem.Common.Adapters.Http.Idempotency;

/// <summary>
/// Makes unsafe requests (POST/PUT/PATCH/DELETE) carrying an Idempotency-Key header safe to retry:
/// the first execution's response is stored and replayed for the same user, method, path, key and body.
/// Must run after authentication so the key is scoped to the caller.
/// </summary>
public sealed class IdempotencyMiddleware
{
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    private static readonly JsonSerializerOptions ProblemSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly IIdempotencyStore _store;
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(
        RequestDelegate next,
        IIdempotencyStore store,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!IsUnsafeMethod(request.Method) ||
            !request.Headers.TryGetValue(_options.HeaderName, out var headerValues) ||
            string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = headerValues.ToString();
        if (idempotencyKey.Length > _options.MaxKeyLength)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Idempotency.InvalidKey",
                $"The {_options.HeaderName} header must be at most {_options.MaxKeyLength} characters.");
            return;
        }

        var ct = context.RequestAborted;
        var storeKey = BuildStoreKey(context, idempotencyKey);
        var requestHash = await HashBodyAsync(request, ct);

        var begin = await _store.TryBeginAsync(storeKey, requestHash, _options.InProgressTtl, ct);
        switch (begin.Status)
        {
            case IdempotencyBeginStatus.Completed:
                _logger.LogInformation("Replaying stored response for idempotency key {Key}", idempotencyKey);
                await ReplayAsync(context, begin.Response!);
                return;

            case IdempotencyBeginStatus.InProgress:
                await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Idempotency.InProgress",
                    "A request with the same idempotency key is still being processed. Retry later.");
                return;

            case IdempotencyBeginStatus.HashMismatch:
                await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity, "Idempotency.KeyReused",
                    "The idempotency key was already used with a different request body.");
                return;
        }

        await ExecuteAndStoreAsync(context, storeKey, requestHash, ct);
    }

    private async Task ExecuteAndStoreAsync(HttpContext context, string storeKey, string requestHash, CancellationToken ct)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            await _store.ReleaseAsync(storeKey, CancellationToken.None);
            throw;
        }

        context.Response.Body = originalBody;
        var body = buffer.ToArray();

        if (context.Response.StatusCode < StatusCodes.Status500InternalServerError)
        {
            var response = new CachedResponse(context.Response.StatusCode, context.Response.ContentType, body);
            await _store.CompleteAsync(storeKey, requestHash, response, _options.ResponseTtl, CancellationToken.None);
        }
        else
        {
            // Server errors are not final: let the client retry with the same key
            await _store.ReleaseAsync(storeKey, CancellationToken.None);
        }

        if (body.Length > 0)
        {
            await originalBody.WriteAsync(body, ct);
        }
    }

    private static async Task ReplayAsync(HttpContext context, CachedResponse cached)
    {
        context.Response.StatusCode = cached.StatusCode;
        if (cached.ContentType is not null)
        {
            context.Response.ContentType = cached.ContentType;
        }

        context.Response.Headers[ReplayedHeaderName] = "true";
        if (cached.Body.Length > 0)
        {
            await context.Response.Body.WriteAsync(cached.Body, context.RequestAborted);
        }
    }

    private static bool IsUnsafeMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static string BuildStoreKey(HttpContext context, string idempotencyKey)
    {
        var user = context.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("sub")?.Value
                     ?? "anonymous";

        var scope = $"{userId}\n{context.Request.Method.ToUpperInvariant()}\n{context.Request.Path.Value}\n{idempotencyKey}";
        return "idempotency:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
    }

    private static async Task<string> HashBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        var hash = await SHA256.HashDataAsync(request.Body, ct);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code }
        };

        return context.Response.WriteAsJsonAsync(problem, ProblemSerializerOptions, "application/problem+json", context.RequestAborted);
    }
}
