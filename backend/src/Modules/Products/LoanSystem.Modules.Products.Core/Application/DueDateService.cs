using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using LoanSystem.Modules.Products.Core.Ports.Outbound;

namespace LoanSystem.Modules.Products.Core.Application;

/// <summary>
/// Builds the business calendar for the loan horizon (holidays + configured weekdays) and generates the
/// due dates with the pure <see cref="DueDateGenerator"/> (PRO-06.1). Used by the product quote (PRO-Q) and by Loans.
/// </summary>
internal sealed class DueDateService
{
    private readonly IHolidayProvider _holidays;
    private readonly ProductsOptions _options;

    public DueDateService(IHolidayProvider holidays, ProductsOptions options)
    {
        _holidays = holidays;
        _options = options;
    }

    public async Task<Result<IReadOnlyList<DueDatePair>>> GenerateAsync(
        DateOnly disbursementDate,
        PaymentFrequency frequency,
        int installments,
        int? paymentDay,
        HolidayShift holidayShift,
        CancellationToken ct = default)
    {
        if (installments < 1)
            return Error.Validation("Products.InvalidTerm", "Installments must be at least 1.");

        var nonBusinessWeekdays = _options.Engine.ResolvedNonBusinessWeekdays;
        var horizon = DueDateGenerator.CalendarHorizon(disbursementDate, frequency, installments, nonBusinessWeekdays.Count);
        var holidays = await _holidays.GetHolidaysAsync(disbursementDate, horizon, ct);
        var calendar = new BusinessCalendar(holidays, nonBusinessWeekdays);

        return DueDateGenerator.Generate(
            new DueDateRequest(disbursementDate, frequency, installments, paymentDay, holidayShift,
                _options.Engine.MinDaysToFirstInstallment),
            calendar);
    }
}
