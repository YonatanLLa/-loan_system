using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;

namespace LoanSystem.Modules.Products.Core.Domain;

internal sealed record ScheduleInstallment(
    int Number,
    Money Principal,
    Money Interest,
    Money Total,
    Money RemainingBalance);

internal sealed record AmortizationSchedule(
    Money Principal,
    Money TotalInterest,
    Money TotalPayment,
    IReadOnlyList<ScheduleInstallment> Installments);

/// <summary>
/// Legacy entry point for the monthly French schedule. The calculation now lives in
/// <see cref="ScheduleEngine"/> / <see cref="FrenchStrategy"/> (BE-PRO-02); this facade keeps the original API
/// and its expected values (DECISION_01) unchanged.
/// </summary>
internal static class FrenchAmortizationSchedule
{
    public static Money CalculateInstallment(Money principal, decimal monthlyRate, int termMonths)
    {
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), "Term must be greater than zero.");

        return FrenchStrategy.CalculateInstallment(principal, monthlyRate, termMonths);
    }

    /// <param name="annualRatePercentage">Annual rate in percent (e.g. 30 = 30%).</param>
    /// <param name="rateType">
    /// How to read the annual rate. In Peru products are quoted as TEA (<see cref="RateType.Efectiva"/>),
    /// which is converted with (1+TEA)^(1/12)-1, NOT TEA/12. See DECISION_01_TASA_EFECTIVA_TEA.md.
    /// </param>
    public static AmortizationSchedule Build(Money principal, decimal annualRatePercentage, int termMonths, RateType rateType)
    {
        if (annualRatePercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRatePercentage), "Rate cannot be negative.");

        var result = ScheduleEngine.Build(new ScheduleInput(
            principal, annualRatePercentage / 100m, rateType, PaymentFrequency.MENSUAL, termMonths, AmortizationMethod.FRANCES));

        if (result.IsFailure)
            throw new InvalidOperationException(result.Error.Description);

        var schedule = result.Value;
        return new AmortizationSchedule(
            schedule.Principal,
            schedule.TotalInterest,
            schedule.TotalPayable,
            schedule.Lines
                .Select(l => new ScheduleInstallment(l.Number, l.Principal, l.Interest, l.Total, l.RemainingBalance))
                .ToList());
    }
}
