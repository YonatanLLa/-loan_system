using System.Data.Common;
using System.Text.Json;
using Dapper;
using LoanSystem.Common.Adapters.Persistence;
using LoanSystem.Common.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;
using LoanSystem.Modules.Loans.Core.Contracts;
using LoanSystem.Modules.Origination.Core.Contracts;

namespace LoanSystem.Modules.Customers.Adapters.Outbound.Persistence;

internal sealed class CustomerHistoryReader : ICustomerHistoryReader
{
    private static readonly HashSet<string> IgnoredAuditFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "CreatedAt",
        "UpdatedAt",
        "Version"
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerHistoryReader(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<CustomerChangeEntry>> GetHistoryAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct) as DbConnection
            ?? throw new InvalidOperationException("Expected DbConnection.");

        var entityId = customerId.ToString();
        var offset = (page - 1) * pageSize;

        const string sql = """
            SELECT COUNT(*)
            FROM audit_logs
            WHERE entity_name = 'customers' AND entity_id = @EntityId;

            SELECT a.created_at AS ChangedAt,
                   a.user_id AS ChangedBy,
                   u.email AS ChangedByEmail,
                   a.action AS Action,
                   a.state_before::text AS StateBefore,
                   a.state_after::text AS StateAfter,
                   host(a.user_ip) AS Ip,
                   a.user_agent AS UserAgent
            FROM audit_logs a
            LEFT JOIN users u ON u.id = a.user_id
            WHERE a.entity_name = 'customers' AND a.entity_id = @EntityId
            ORDER BY a.created_at DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { EntityId = entityId, PageSize = pageSize, Offset = offset }, cancellationToken: ct));

        var totalCount = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<AuditHistoryRow>()).ToList();

        var items = rows.Select(r => new CustomerChangeEntry(
            r.ChangedAt,
            r.ChangedBy,
            r.ChangedByEmail,
            r.Action,
            ComputeChanges(r.StateBefore, r.StateAfter),
            r.Ip,
            r.UserAgent)).ToArray();

        return new PagedResult<CustomerChangeEntry>(items, page, pageSize, totalCount);
    }

    private static IReadOnlyList<FieldChange> ComputeChanges(string? stateBeforeJson, string? stateAfterJson)
    {
        if (string.IsNullOrWhiteSpace(stateAfterJson))
        {
            return [];
        }

        using var afterDoc = JsonDocument.Parse(stateAfterJson);
        if (afterDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(stateBeforeJson))
        {
            var initial = new List<FieldChange>();
            foreach (var prop in afterDoc.RootElement.EnumerateObject())
            {
                if (IgnoredAuditFields.Contains(prop.Name))
                {
                    continue;
                }

                var afterValue = FormatJsonValue(prop.Value);
                if (afterValue is not null)
                {
                    initial.Add(new FieldChange(prop.Name, null, afterValue));
                }
            }

            return initial;
        }

        using var beforeDoc = JsonDocument.Parse(stateBeforeJson);
        if (beforeDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var changes = new List<FieldChange>();
        foreach (var afterProp in afterDoc.RootElement.EnumerateObject())
        {
            if (IgnoredAuditFields.Contains(afterProp.Name))
            {
                continue;
            }

            string? beforeValue = beforeDoc.RootElement.TryGetProperty(afterProp.Name, out var bp)
                ? FormatJsonValue(bp)
                : null;
            var afterValue = FormatJsonValue(afterProp.Value);

            if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
            {
                changes.Add(new FieldChange(afterProp.Name, beforeValue, afterValue));
            }
        }

        return changes;
    }

    private static string? FormatJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText()
    };

    private sealed record AuditHistoryRow(
        DateTimeOffset ChangedAt,
        Guid? ChangedBy,
        string? ChangedByEmail,
        string Action,
        string? StateBefore,
        string? StateAfter,
        string? Ip,
        string? UserAgent);
}

internal sealed class CustomerPortfolioReader : ICustomerPortfolioReader
{
    private readonly ILoanPortfolioQuery _loanPortfolio;
    private readonly IApplicationHistoryQuery _applicationHistory;

    public CustomerPortfolioReader(
        ILoanPortfolioQuery loanPortfolio,
        IApplicationHistoryQuery applicationHistory)
    {
        _loanPortfolio = loanPortfolio;
        _applicationHistory = applicationHistory;
    }

    public async Task<CustomerPortfolio> GetAsync(Guid customerId, CancellationToken ct = default)
    {
        var loansTask = _loanPortfolio.GetByCustomerAsync(customerId, ct);
        var appsTask = _applicationHistory.GetByCustomerAsync(customerId, ct);

        await Task.WhenAll(loansTask, appsTask);

        var loans = loansTask.Result.Select(l => new CustomerLoanSummary(
            l.LoanId,
            l.LoanNumber,
            l.ProductName,
            l.Status,
            l.Currency,
            l.PrincipalBalance,
            l.InterestBalance,
            l.LateFeeBalance,
            l.DaysPastDue,
            l.MaxDaysPastDue,
            l.NextDueDate,
            l.DisbursedAt)).ToArray();

        var apps = appsTask.Result.Select(a => new CustomerApplicationSummary(
            a.ApplicationId,
            a.ApplicationNumber,
            a.ProductName,
            a.RequestedAmount,
            a.Currency,
            a.TermMonths,
            a.Status,
            a.SubmittedAt)).ToArray();

        return new CustomerPortfolio(loans, apps);
    }
}

internal sealed class KycExpirationBatchStore : IKycExpirationBatchStore
{
    private const string ExpiringSoonEventType =
        "LoanSystem.Modules.Customers.Core.Contracts.IntegrationEvents.CustomerKycExpiringSoonIntegrationEvent";

    private readonly IDbConnectionFactory _connectionFactory;

    public KycExpirationBatchStore(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> ExpireDueBatchAsync(
        DateTimeOffset utcNow,
        int batchSize,
        CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct) as DbConnection
            ?? throw new InvalidOperationException("Expected DbConnection.");

        const string sql = """
            UPDATE customers
            SET kyc_status = 'EXPIRED'::kyc_status_type,
                updated_at = @Now
            WHERE id IN (
                SELECT id
                FROM customers
                WHERE kyc_status = 'VERIFIED'::kyc_status_type
                  AND kyc_expires_at <= @Now
                ORDER BY kyc_expires_at
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id AS CustomerId, kyc_expires_at AS ExpiresAt;
            """;

        var rows = await conn.QueryAsync<KycExpirationRow>(
            new CommandDefinition(sql, new { Now = utcNow.ToUniversalTime(), BatchSize = batchSize }, cancellationToken: ct));

        return rows.Select(r => (r.CustomerId, r.ExpiresAt)).ToArray();
    }

    public async Task<IReadOnlyList<(Guid CustomerId, DateTimeOffset ExpiresAt)>> FindExpiringSoonAsync(
        DateTimeOffset utcNow,
        DateTimeOffset windowEndUtc,
        int batchSize,
        CancellationToken ct = default)
    {
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct) as DbConnection
            ?? throw new InvalidOperationException("Expected DbConnection.");

        // Skip customers that already received an expiring-soon event in the recent 30-day window.
        var recentCutoff = utcNow.AddDays(-30).ToUniversalTime();

        const string sql = """
            SELECT c.id AS CustomerId, c.kyc_expires_at AS ExpiresAt
            FROM customers c
            WHERE c.kyc_status = 'VERIFIED'::kyc_status_type
              AND c.kyc_expires_at > @Now
              AND c.kyc_expires_at <= @WindowEnd
              AND NOT EXISTS (
                  SELECT 1
                  FROM outbox_messages o
                  WHERE o.event_type = @EventType
                    AND o.occurred_on >= @RecentCutoff
                    AND o.payload ->> 'customerId' = c.id::text
              )
            ORDER BY c.kyc_expires_at
            LIMIT @BatchSize;
            """;

        var rows = await conn.QueryAsync<KycExpirationRow>(
            new CommandDefinition(
                sql,
                new
                {
                    Now = utcNow.ToUniversalTime(),
                    WindowEnd = windowEndUtc.ToUniversalTime(),
                    EventType = ExpiringSoonEventType,
                    RecentCutoff = recentCutoff,
                    BatchSize = batchSize
                },
                cancellationToken: ct));

        return rows.Select(r => (r.CustomerId, r.ExpiresAt)).ToArray();
    }

    private sealed record KycExpirationRow(Guid CustomerId, DateTimeOffset ExpiresAt);
}
