using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Clock;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Security.Core.Contracts;
using LoanSystem.Modules.Security.Core.Ports.Outbound;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class AuditLogReader : IAuditLogReader
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogReader(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PagedResult<AuditLogItem>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        var whereClauses = new List<string>();

        var entity = query.ResolvedEntity;
        if (!string.IsNullOrWhiteSpace(entity))
        {
            whereClauses.Add("a.entity_name = @EntityName");
            parameters.Add("EntityName", entity);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            whereClauses.Add("a.entity_id = @EntityId");
            parameters.Add("EntityId", query.EntityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            whereClauses.Add("a.action = @Action");
            parameters.Add("Action", query.Action.Trim().ToUpperInvariant());
        }

        if (query.UserId.HasValue)
        {
            whereClauses.Add("a.user_id = @UserId");
            parameters.Add("UserId", query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.User))
        {
            var escapedUser = query.User.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            whereClauses.Add("(u.email ILIKE @UserSearch ESCAPE '\\' OR u.full_name ILIKE @UserSearch ESCAPE '\\')");
            parameters.Add("UserSearch", $"%{escapedUser}%");
        }

        if (query.From.HasValue)
        {
            var fromDateTime = query.From.Value.ToDateTime(TimeOnly.MinValue);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDateTime, LimaTimeZone.Instance);
            whereClauses.Add("a.created_at >= @FromUtc");
            parameters.Add("FromUtc", fromUtc);
        }

        if (query.To.HasValue)
        {
            var toExclusiveDateTime = query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var toExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(toExclusiveDateTime, LimaTimeZone.Instance);
            whereClauses.Add("a.created_at < @ToExclusiveUtc");
            parameters.Add("ToExclusiveUtc", toExclusiveUtc);
        }

        var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : string.Empty;

        var countJoin = !string.IsNullOrWhiteSpace(query.User) ? "LEFT JOIN users u ON a.user_id = u.id" : string.Empty;
        var countSql = $"SELECT COUNT(*) FROM audit_logs a {countJoin} {whereSql};";

        var offset = (query.Page - 1) * query.PageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", query.PageSize);

        var itemsSql = $"""
            SELECT
                a.id AS Id,
                a.entity_name AS EntityName,
                a.entity_id AS EntityId,
                a.action AS Action,
                a.state_before AS StateBefore,
                a.state_after AS StateAfter,
                a.user_id AS UserId,
                u.full_name AS UserName,
                u.email AS UserEmail,
                host(a.user_ip) AS UserIp,
                a.user_agent AS UserAgent,
                a.created_at AS CreatedAt
            FROM audit_logs a
            LEFT JOIN users u ON a.user_id = u.id
            {whereSql}
            ORDER BY a.created_at DESC, a.id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var rows = await connection.QueryAsync<AuditLogRow>(new CommandDefinition(itemsSql, parameters, cancellationToken: ct));

        var items = rows.Select(r => new AuditLogItem(
            r.Id,
            r.EntityName,
            r.EntityId,
            r.Action,
            ParseJsonElement(r.StateBefore),
            ParseJsonElement(r.StateAfter),
            r.UserId,
            r.UserName,
            r.UserEmail,
            r.UserIp,
            r.UserAgent,
            r.CreatedAt
        )).ToList();

        return new PagedResult<AuditLogItem>(items, total, query.Page, query.PageSize);
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private sealed class AuditLogRow
    {
        public long Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? StateBefore { get; set; }
        public string? StateAfter { get; set; }
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? UserIp { get; set; }
        public string? UserAgent { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
