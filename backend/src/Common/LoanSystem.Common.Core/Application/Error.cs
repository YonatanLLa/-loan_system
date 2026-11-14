using System.Collections.ObjectModel;

namespace LoanSystem.Common.Core.Application;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Unprocessable = 6,
    Locked = 7
}

public sealed record Error(
    string Code,
    string Description,
    ErrorType Type = ErrorType.Failure,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
    public static Error Unprocessable(string code, string description) => new(code, description, ErrorType.Unprocessable);
    public static Error Locked(string code, string description) => new(code, description, ErrorType.Locked);

    /// <summary>Returns a copy of this error with an additional ProblemDetails extension field.</summary>
    public Error WithExtension(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var dict = Extensions is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(Extensions, StringComparer.Ordinal);
        dict[key] = value;
        return this with { Extensions = new ReadOnlyDictionary<string, object?>(dict) };
    }

    /// <summary>Returns a copy of this error with additional ProblemDetails extension fields merged in.</summary>
    public Error WithExtensions(IReadOnlyDictionary<string, object?> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var dict = Extensions is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(Extensions, StringComparer.Ordinal);
        foreach (var (key, value) in extensions)
        {
            dict[key] = value;
        }
        return this with { Extensions = new ReadOnlyDictionary<string, object?>(dict) };
    }
}
