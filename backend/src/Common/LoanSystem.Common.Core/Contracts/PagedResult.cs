namespace LoanSystem.Common.Core.Contracts;

/// <summary>
/// Universal paged response contract matching frontend Page&lt;T&gt; shape (items, total, page, pageSize).
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
