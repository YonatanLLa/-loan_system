using System.Security.Claims;
using System.Text;
using LoanSystem.Common.Adapters.Http.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class IdempotencyMiddlewareTests
{
    private readonly InMemoryIdempotencyStore _store = new();
    private int _executions;

    [Fact]
    public async Task FirstRequest_Executes_AndReplay_ReturnsSameResponse()
    {
        var middleware = CreateMiddleware(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync($"{{\"execution\":{_executions}}}");
        });

        var first = CreateContext("{\"amount\":100}");
        await middleware.InvokeAsync(first);

        var replay = CreateContext("{\"amount\":100}");
        await middleware.InvokeAsync(replay);

        _executions.ShouldBe(1);
        first.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
        first.Response.Headers.ContainsKey(IdempotencyMiddleware.ReplayedHeaderName).ShouldBeFalse();
        ReadBody(first).ShouldBe("{\"execution\":1}");

        replay.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
        replay.Response.ContentType.ShouldBe("application/json");
        replay.Response.Headers[IdempotencyMiddleware.ReplayedHeaderName].ToString().ShouldBe("true");
        ReadBody(replay).ShouldBe("{\"execution\":1}");
    }

    [Fact]
    public async Task SameKey_WithDifferentBody_Returns422()
    {
        var middleware = CreateMiddleware(ctx => ctx.Response.WriteAsync("ok"));

        await middleware.InvokeAsync(CreateContext("{\"amount\":100}"));
        var second = CreateContext("{\"amount\":999}");
        await middleware.InvokeAsync(second);

        _executions.ShouldBe(1);
        second.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        second.Response.ContentType.ShouldBe("application/problem+json");
        ReadBody(second).ShouldContain("Idempotency.KeyReused");
    }

    [Fact]
    public async Task ConcurrentDuplicate_WhileFirstInProgress_Returns409()
    {
        var release = new TaskCompletionSource();
        var middleware = CreateMiddleware(async ctx =>
        {
            await release.Task;
            await ctx.Response.WriteAsync("done");
        });

        var firstTask = middleware.InvokeAsync(CreateContext("{}"));

        var duplicate = CreateContext("{}");
        await middleware.InvokeAsync(duplicate);

        duplicate.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        ReadBody(duplicate).ShouldContain("Idempotency.InProgress");

        release.SetResult();
        await firstTask;
        _executions.ShouldBe(1);
    }

    [Fact]
    public async Task ServerError_IsNotCached_SoRetryExecutesAgain()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = _executions == 1
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        var first = CreateContext("{}");
        await middleware.InvokeAsync(first);
        var retry = CreateContext("{}");
        await middleware.InvokeAsync(retry);

        _executions.ShouldBe(2);
        first.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        retry.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        retry.Response.Headers.ContainsKey(IdempotencyMiddleware.ReplayedHeaderName).ShouldBeFalse();
    }

    [Fact]
    public async Task Exception_ReleasesKey_SoRetryExecutesAgain()
    {
        var middleware = CreateMiddleware(ctx =>
            _executions == 1 ? throw new InvalidOperationException("boom") : ctx.Response.WriteAsync("ok"));

        await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(CreateContext("{}")));
        var retry = CreateContext("{}");
        await middleware.InvokeAsync(retry);

        _executions.ShouldBe(2);
        ReadBody(retry).ShouldBe("ok");
    }

    [Fact]
    public async Task SameKey_FromDifferentUsers_IsNotShared()
    {
        var middleware = CreateMiddleware(ctx => ctx.Response.WriteAsync("ok"));

        await middleware.InvokeAsync(CreateContext("{}", userId: "user-a"));
        var otherUser = CreateContext("{}", userId: "user-b");
        await middleware.InvokeAsync(otherUser);

        _executions.ShouldBe(2);
        otherUser.Response.Headers.ContainsKey(IdempotencyMiddleware.ReplayedHeaderName).ShouldBeFalse();
    }

    [Theory]
    [InlineData("GET", "key-1")]
    [InlineData("POST", null)]
    public async Task SafeMethods_OrMissingHeader_AreNotTracked(string method, string? key)
    {
        var middleware = CreateMiddleware(ctx => ctx.Response.WriteAsync("ok"));

        await middleware.InvokeAsync(CreateContext("{}", key, method));
        var second = CreateContext("{}", key, method);
        await middleware.InvokeAsync(second);

        _executions.ShouldBe(2);
        second.Response.Headers.ContainsKey(IdempotencyMiddleware.ReplayedHeaderName).ShouldBeFalse();
    }

    [Fact]
    public async Task TooLongKey_Returns400()
    {
        var middleware = CreateMiddleware(ctx => ctx.Response.WriteAsync("ok"));

        var context = CreateContext("{}", new string('k', 256));
        await middleware.InvokeAsync(context);

        _executions.ShouldBe(0);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    private IdempotencyMiddleware CreateMiddleware(RequestDelegate endpoint) =>
        new(ctx =>
            {
                _executions++;
                return endpoint(ctx);
            },
            _store,
            Options.Create(new IdempotencyOptions()),
            NullLogger<IdempotencyMiddleware>.Instance);

    private static DefaultHttpContext CreateContext(string body, string? key = "key-1", string method = "POST", string? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/products/simulate";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (key is not null)
        {
            context.Request.Headers["Idempotency-Key"] = key;
        }

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        }

        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
