using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Products.Core.Domain.Engine;

/// <summary>Theoretical due date (interest is accrued up to it, plan D4) and the enforceable one (shifted by holidays).</summary>
internal sealed record DueDatePair(DateOnly TheoreticalDueDate, DateOnly DueDate);

/// <summary>
/// Input of the pure schedule engine (PRO-02.4). Nothing here touches the database or the clock (plan D14).
/// </summary>
/// <param name="Principal">Financed principal (already includes the opening fee when it is FINANCIADA).</param>
/// <param name="AnnualRateFraction">Annual rate as a fraction (0.30 = 30 %).</param>
/// <param name="DisbursementDate">
/// <c>null</c> → <see cref="DayCountMode.NOMINAL_PERIODS"/>; a value → <see cref="DayCountMode.ACTUAL_360"/>
/// and <paramref name="DueDates"/> becomes mandatory (one pair per installment, produced by the due date generator of BE-PRO-06).
/// </param>
/// <param name="PeriodicFee">Fixed charge per installment (PRO-03.3). It does not accrue interest. <c>null</c> = 0.</param>
internal sealed record ScheduleInput(
    Money Principal,
    decimal AnnualRateFraction,
    RateType RateType,
    PaymentFrequency Frequency,
    int Installments,
    AmortizationMethod Method,
    DateOnly? DisbursementDate = null,
    IReadOnlyList<DueDatePair>? DueDates = null,
    Money? PeriodicFee = null);

internal sealed record ScheduleLine(
    int Number,
    DateOnly? TheoreticalDueDate,
    DateOnly? DueDate,
    int Days,
    Money Principal,
    Money Interest,
    Money Fees,
    Money Total,
    Money RemainingBalance);

/// <param name="ReferencePeriodRate">Rate of a nominal period of the frequency (1/7/15/30 days). Not rounded.</param>
/// <param name="InstallmentAmount">Total of the first installment (principal + interest + fees).</param>
/// <param name="EffectiveAnnualRate">
/// Only for FLAT: real effective annual rate of the principal + interest flow (IRR, plan D12). <c>null</c> otherwise.
/// </param>
internal sealed record ScheduleResult(
    DayCountMode Mode,
    AmortizationMethod Method,
    PaymentFrequency Frequency,
    decimal ReferencePeriodRate,
    Money Principal,
    Money InstallmentAmount,
    Money TotalInterest,
    Money TotalFees,
    Money TotalPayable,
    decimal? EffectiveAnnualRate,
    IReadOnlyList<ScheduleLine> Lines);

/// <summary>What an amortization strategy receives: principal, number of installments and the rate of every period.</summary>
/// <param name="PeriodRates">i_k for k = 1..n (unrounded). In NOMINAL_PERIODS all are equal.</param>
/// <param name="NominalPeriodRate">Rate with the nominal days of the frequency (used by FLAT in every mode).</param>
internal sealed record AmortizationContext(
    Money Principal,
    int Installments,
    DayCountMode Mode,
    IReadOnlyList<decimal> PeriodRates,
    decimal NominalPeriodRate);

internal sealed record AmortizationRow(Money Principal, Money Interest);
