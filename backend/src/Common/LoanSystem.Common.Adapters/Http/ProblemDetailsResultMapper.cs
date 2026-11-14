using LoanSystem.Common.Core.Application;
using Microsoft.AspNetCore.Http;

namespace LoanSystem.Common.Adapters.Http;

public static class ProblemDetailsResultMapper
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok();
        }

        return ToProblemDetails(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblemDetails(result.Error);
    }

    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> locationFactory)
    {
        ArgumentNullException.ThrowIfNull(locationFactory);

        if (result.IsSuccess)
        {
            return Results.Created(locationFactory(result.Value), result.Value);
        }

        return ToProblemDetails(result.Error);
    }

    public static IResult ToAcceptedResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Accepted(value: result.Value);
        }

        return ToProblemDetails(result.Error);
    }

    private static IResult ToProblemDetails(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Locked => StatusCodes.Status423Locked,
            _ => StatusCodes.Status500InternalServerError
        };

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
            ["errorType"] = error.Type.ToString()
        };

        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                if (key is not "code" and not "errorType")
                {
                    extensions[key] = value;
                }
            }
        }

        return Results.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description,
            extensions: extensions);
    }
}
