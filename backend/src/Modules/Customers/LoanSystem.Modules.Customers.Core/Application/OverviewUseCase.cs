using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Common.Core.Ports.Outbound;
using LoanSystem.Modules.Customers.Core.Contracts;
using LoanSystem.Modules.Customers.Core.Domain;
using LoanSystem.Modules.Customers.Core.Ports.Inbound;
using LoanSystem.Modules.Customers.Core.Ports.Outbound;

namespace LoanSystem.Modules.Customers.Core.Application;

internal sealed class GetCustomerOverviewUseCase : IGetCustomerOverviewUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly ICustomerPortfolioReader _portfolioReader;
    private readonly ICurrentUser _currentUser;

    public GetCustomerOverviewUseCase(
        ICustomerRepository customers,
        ICustomerPortfolioReader portfolioReader,
        ICurrentUser currentUser)
    {
        _customers = customers;
        _portfolioReader = portfolioReader;
        _currentUser = currentUser;
    }

    public async Task<Result<CustomerOverviewResponse>> ExecuteAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.FindByIdAsync(customerId, ct);
        if (customer is null)
        {
            return CustomerErrors.NotFound(customerId);
        }

        var portfolio = await _portfolioReader.GetAsync(customerId, ct);

        var activeLoans = portfolio.Loans
            .Where(l => string.Equals(l.Status, "VIGENTE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(l.Status, "EN_MORA", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var closedLoans = portfolio.Loans
            .Where(l => !string.Equals(l.Status, "VIGENTE", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(l.Status, "EN_MORA", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var currency = activeLoans.Select(l => l.Currency).FirstOrDefault() ?? Money.DefaultCurrency;
        var principalSum = Money.Round(activeLoans.Sum(l => Money.Round(l.PrincipalBalance)));
        var interestSum = Money.Round(activeLoans.Sum(l => Money.Round(l.InterestBalance)));
        var lateFeeSum = Money.Round(activeLoans.Sum(l => Money.Round(l.LateFeeBalance)));
        var totalDebt = Money.Round(principalSum + interestSum + lateFeeSum);

        var debt = new ConsolidatedDebt(
            Money.From(principalSum, currency),
            Money.From(interestSum, currency),
            Money.From(lateFeeSum, currency),
            Money.From(totalDebt, currency));

        var currentMaxDpd = activeLoans.Length > 0 ? activeLoans.Max(l => l.DaysPastDue) : 0;
        var historicalMaxDpd = portfolio.Loans.Count > 0 ? portfolio.Loans.Max(l => l.MaxDaysPastDue) : 0;
        var hasActiveDelinquency = activeLoans.Any(l => l.DaysPastDue > 0);

        var applications = portfolio.Applications
            .OrderByDescending(a => a.SubmittedAt)
            .ToArray();

        return new CustomerOverviewResponse(
            CustomerMapper.ToResponse(customer, _currentUser),
            activeLoans,
            closedLoans,
            debt,
            currentMaxDpd,
            historicalMaxDpd,
            hasActiveDelinquency,
            applications,
            Money.Round(customer.CreditBalance));
    }
}
