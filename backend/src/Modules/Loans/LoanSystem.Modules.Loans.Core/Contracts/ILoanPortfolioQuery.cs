namespace LoanSystem.Modules.Loans.Core.Contracts;

public sealed record LoanSummary(
    Guid LoanId,
    string LoanNumber,
    string ProductName,
    string Status,
    string Currency,
    decimal PrincipalBalance,
    decimal InterestBalance,
    decimal LateFeeBalance,
    int DaysPastDue,
    int MaxDaysPastDue,
    DateOnly? NextDueDate,
    DateTimeOffset DisbursedAt);

/// <summary>
/// Read-only portfolio query contract exposed by Loans for cross-module readers such as Customers Overview (BE-CUS-08).
/// </summary>
public interface ILoanPortfolioQuery
{
    Task<IReadOnlyList<LoanSummary>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
}
