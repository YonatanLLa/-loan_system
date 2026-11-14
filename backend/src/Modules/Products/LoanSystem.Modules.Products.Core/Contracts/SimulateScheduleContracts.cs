namespace LoanSystem.Modules.Products.Core.Contracts;

/// <summary>
/// Quick simulation without a product (always NOMINAL_PERIODS, no dates).
/// </summary>
/// <param name="AnnualRatePercentage">Annual rate in percent (30 = 30 %).</param>
/// <param name="TermMonths">Number of installments (kept for compatibility; prefer <paramref name="Installments"/>).</param>
/// <param name="Method">FRANCES (alias FRENCH), FLAT, ALEMAN or AMERICANO.</param>
/// <param name="RateType">EFECTIVA (TEA) or NOMINAL (TNA).</param>
/// <param name="Frequency">DIARIA, SEMANAL, QUINCENAL or MENSUAL (default).</param>
/// <param name="Installments">Number of installments; wins over <paramref name="TermMonths"/> when present.</param>
public sealed record SimulateScheduleRequest(
    decimal Amount,
    decimal AnnualRatePercentage,
    int TermMonths = 0,
    string Method = "FRENCH",
    string RateType = "EFECTIVA",
    string Frequency = "MENSUAL",
    int? Installments = null);

public sealed record InstallmentSimulation(
    int Number,
    decimal Principal,
    decimal Interest,
    decimal Total,
    decimal RemainingBalance);

/// <param name="PeriodRatePercentage">Rate of each period in percent, unrounded.</param>
/// <param name="InstallmentAmount">Amount of the first installment.</param>
/// <param name="EffectiveTeaPercentage">Only for FLAT: real effective annual rate in percent (2 decimals).</param>
public sealed record SimulateScheduleResponse(
    decimal Amount,
    decimal TotalInterest,
    decimal TotalPayment,
    IReadOnlyList<InstallmentSimulation> Installments,
    string Method = "FRANCES",
    string Frequency = "MENSUAL",
    decimal PeriodRatePercentage = 0m,
    decimal InstallmentAmount = 0m,
    decimal? EffectiveTeaPercentage = null);
