namespace LoanSystem.Modules.Products.Core.Contracts;

public sealed record SimulateScheduleRequest(
    decimal Amount,
    decimal AnnualRatePercentage,
    int TermMonths,
    string Method = "FRENCH",
    string RateType = "EFECTIVA");

public sealed record InstallmentSimulation(
    int Number,
    decimal Principal,
    decimal Interest,
    decimal Total,
    decimal RemainingBalance);

public sealed record SimulateScheduleResponse(
    decimal Amount,
    decimal TotalInterest,
    decimal TotalPayment,
    IReadOnlyList<InstallmentSimulation> Installments);
