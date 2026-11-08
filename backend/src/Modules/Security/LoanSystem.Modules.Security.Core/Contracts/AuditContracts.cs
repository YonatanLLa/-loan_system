using System.Text.Json;

namespace LoanSystem.Modules.Security.Core.Contracts;

/// <summary>
/// Query filters for audit log inspection (BE-SEC-08).
/// Supports both backoffice frontend parameter names ('entity', 'user') and explicit schema names ('entityName', 'userId').
/// </summary>
public sealed record AuditLogQuery(
    string? Entity = null,
    string? EntityName = null,
    string? EntityId = null,
    string? Action = null,
    Guid? UserId = null,
    string? User = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50)
{
    public string? ResolvedEntity => !string.IsNullOrWhiteSpace(Entity) ? Entity.Trim() : (!string.IsNullOrWhiteSpace(EntityName) ? EntityName.Trim() : null);
}

/// <summary>
/// Individual audit log record with user details and unescaped JSON state snapshots (BE-SEC-08).
/// </summary>
public sealed record AuditLogItem(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    JsonElement? StateBefore,
    JsonElement? StateAfter,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    string? UserIp,
    string? UserAgent,
    DateTimeOffset CreatedAt);
