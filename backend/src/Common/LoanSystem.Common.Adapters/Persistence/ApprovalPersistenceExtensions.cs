using LoanSystem.Common.Core.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystem.Common.Adapters.Persistence;

/// <summary>
/// Translates PostgreSQL unique-index and optimistic-concurrency conflicts on <c>approval_requests</c>
/// into domain <see cref="Result"/> errors (BE-SEC-09, R6, D9).
/// </summary>
public static class ApprovalPersistenceExtensions
{
    private const string UniqueViolationSqlState = "23505";
    private const string PendingTargetIndexName = "ux_approval_requests_pending_target";

    public static async Task<Result> SaveApprovalChangesAsync(this DbContext db, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict(
                "Approvals.ConcurrentDecision",
                "Another user already decided or modified this approval request.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && string.Equals(pg.SqlState, UniqueViolationSqlState, StringComparison.Ordinal)
            && string.Equals(pg.ConstraintName, PendingTargetIndexName, StringComparison.Ordinal))
        {
            return Error.Conflict(
                "Approvals.DuplicatePending",
                "There is already a pending approval request for this operation and target.");
        }
    }
}
