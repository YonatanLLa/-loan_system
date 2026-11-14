using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Common.Core.Contracts.Approvals;
using LoanSystem.Common.Core.Domain.Approvals;
using LoanSystem.Common.Core.Ports.Outbound.Approvals;

namespace LoanSystem.Modules.Security.Adapters.Outbound.Persistence;

internal sealed class ApprovalRequestReader : IApprovalRequestReader
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApprovalRequestReader(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<ApprovalRequestHeader?> GetHeaderAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id             AS Id,
                operation_type AS OperationType,
                module         AS Module,
                target_entity  AS TargetEntity,
                target_id      AS TargetId,
                status         AS Status,
                requested_by   AS RequestedBy,
                expires_at     AS ExpiresAt,
                payload        AS PayloadJson
            FROM approval_requests
            WHERE id = @Id;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<HeaderRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (row is null || !Enum.TryParse<ApprovalStatus>(row.Status, ignoreCase: true, out var status))
            return null;

        return new ApprovalRequestHeader(
            row.Id,
            row.OperationType,
            row.Module,
            row.TargetEntity,
            row.TargetId,
            status,
            row.RequestedBy,
            row.ExpiresAt,
            row.PayloadJson);
    }

    public async Task<ApprovalRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                a.id               AS Id,
                a.operation_type   AS OperationType,
                a.target_entity    AS TargetEntity,
                a.target_id        AS TargetId,
                a.status           AS Status,
                a.payload          AS PayloadJson,
                a.target_snapshot  AS TargetSnapshotJson,
                a.reason           AS Reason,
                a.evidence_ref     AS EvidenceRef,
                a.requested_by     AS RequestedById,
                req.full_name      AS RequestedByName,
                req.email          AS RequestedByEmail,
                a.requested_at     AS RequestedAt,
                a.expires_at       AS ExpiresAt,
                a.decided_by       AS DecidedById,
                dec.full_name      AS DecidedByName,
                dec.email          AS DecidedByEmail,
                a.decided_at       AS DecidedAt,
                a.decision_comment AS DecisionComment
            FROM approval_requests a
            INNER JOIN users req ON a.requested_by = req.id
            LEFT JOIN users dec  ON a.decided_by = dec.id
            WHERE a.id = @Id;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<DetailRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (row is null)
            return null;

        JsonElement? currentTargetState = null;
        if (string.Equals(row.TargetEntity, "users", StringComparison.OrdinalIgnoreCase))
        {
            const string userSql = """
                SELECT json_build_object(
                    'role', role::text,
                    'status', status::text,
                    'email', email,
                    'fullName', full_name
                )::text
                FROM users
                WHERE id = @TargetId;
                """;
            var userJson = await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(userSql, new { row.TargetId }, cancellationToken: ct));
            currentTargetState = ParseNullableJson(userJson);
        }

        return new ApprovalRequestDto(
            row.Id,
            row.OperationType,
            row.TargetEntity,
            row.TargetId,
            row.Status,
            ParseJson(row.PayloadJson),
            ParseJson(row.TargetSnapshotJson),
            currentTargetState,
            row.Reason,
            row.EvidenceRef,
            new ApprovalUserRef(row.RequestedById, row.RequestedByName, row.RequestedByEmail),
            row.RequestedAt,
            row.ExpiresAt,
            row.DecidedById.HasValue
                ? new ApprovalUserRef(row.DecidedById.Value, row.DecidedByName ?? string.Empty, row.DecidedByEmail ?? string.Empty)
                : null,
            row.DecidedAt,
            row.DecisionComment);
    }

    public async Task<PagedResult<ApprovalRequestSummary>> QueryAsync(
        ApprovalQuery query, Guid currentUserId, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        var where = new List<string>();
        var scope = query.Scope?.Trim().ToLowerInvariant();

        if (scope == "todecide")
        {
            where.Add("a.requested_by <> @CurrentUserId");
            where.Add("NOT (a.operation_type = 'USER_ROLE_CHANGE' AND a.target_id = @CurrentUserId)");
            parameters.Add("CurrentUserId", currentUserId);

            if (string.IsNullOrWhiteSpace(query.Status))
            {
                where.Add("a.status = 'PENDING'");
            }
        }
        else if (scope == "mine")
        {
            where.Add("a.requested_by = @CurrentUserId");
            parameters.Add("CurrentUserId", currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            where.Add("a.status = @Status");
            parameters.Add("Status", query.Status.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.OperationType))
        {
            where.Add("a.operation_type = @OperationType");
            parameters.Add("OperationType", query.OperationType.Trim().ToUpperInvariant());
        }

        if (query.TargetId.HasValue)
        {
            where.Add("a.target_id = @TargetId");
            parameters.Add("TargetId", query.TargetId.Value);
        }

        if (query.RequestedBy.HasValue)
        {
            where.Add("a.requested_by = @RequestedBy");
            parameters.Add("RequestedBy", query.RequestedBy.Value);
        }

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        var countSql = $"SELECT COUNT(*) FROM approval_requests a {whereSql};";

        var offset = (query.Page - 1) * query.PageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", query.PageSize);

        var itemsSql = $"""
            SELECT
                a.id             AS Id,
                a.operation_type AS OperationType,
                a.target_entity  AS TargetEntity,
                a.target_id      AS TargetId,
                a.status         AS Status,
                a.reason         AS Reason,
                a.evidence_ref   AS EvidenceRef,
                a.requested_by   AS RequestedById,
                req.full_name    AS RequestedByName,
                req.email        AS RequestedByEmail,
                a.requested_at   AS RequestedAt,
                a.expires_at     AS ExpiresAt,
                a.decided_by     AS DecidedById,
                dec.full_name    AS DecidedByName,
                dec.email        AS DecidedByEmail,
                a.decided_at     AS DecidedAt
            FROM approval_requests a
            INNER JOIN users req ON a.requested_by = req.id
            LEFT JOIN users dec  ON a.decided_by = dec.id
            {whereSql}
            ORDER BY a.requested_at DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var rows = await conn.QueryAsync<SummaryRow>(new CommandDefinition(itemsSql, parameters, cancellationToken: ct));

        var items = rows.Select(r => new ApprovalRequestSummary(
            r.Id,
            r.OperationType,
            r.TargetEntity,
            r.TargetId,
            r.Status,
            r.Reason,
            r.EvidenceRef,
            new ApprovalUserRef(r.RequestedById, r.RequestedByName, r.RequestedByEmail),
            r.RequestedAt,
            r.ExpiresAt,
            r.DecidedById.HasValue
                ? new ApprovalUserRef(r.DecidedById.Value, r.DecidedByName ?? string.Empty, r.DecidedByEmail ?? string.Empty)
                : null,
            r.DecidedAt)).ToList();

        return new PagedResult<ApprovalRequestSummary>(items, total, query.Page, query.PageSize);
    }

    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static JsonElement? ParseNullableJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class HeaderRow
    {
        public Guid Id { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string TargetEntity { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid RequestedBy { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string PayloadJson { get; set; } = "{}";
    }

    private sealed class SummaryRow
    {
        public Guid Id { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string TargetEntity { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? EvidenceRef { get; set; }
        public Guid RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public string RequestedByEmail { get; set; } = string.Empty;
        public DateTimeOffset RequestedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public Guid? DecidedById { get; set; }
        public string? DecidedByName { get; set; }
        public string? DecidedByEmail { get; set; }
        public DateTimeOffset? DecidedAt { get; set; }
    }

    private sealed class DetailRow : SummaryRow
    {
        public string PayloadJson { get; set; } = "{}";
        public string TargetSnapshotJson { get; set; } = "{}";
        public string? DecisionComment { get; set; }
    }
}
