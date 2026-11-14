using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Contracts;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Domain.Engine;
using LoanSystem.Modules.Products.Core.Ports.Inbound;

namespace LoanSystem.Modules.Products.Core.Application;

/// <summary>
/// Anonymous quick simulator (PRO-02.8). Always NOMINAL_PERIODS: no product, no dates, no fees.
/// </summary>
internal sealed class SimulateScheduleUseCase : ISimulateScheduleUseCase
{
    internal const int MaxInstallments = 720;
    internal const decimal MaxAmount = 10_000_000m;

    public Task<Result<SimulateScheduleResponse>> ExecuteAsync(SimulateScheduleRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request, out var installments, out var method, out var rateType, out var frequency);
        if (validation is not null)
            return Task.FromResult<Result<SimulateScheduleResponse>>(validation);

        var result = ScheduleEngine.Build(new ScheduleInput(
            Principal: Money.From(request.Amount),
            AnnualRateFraction: request.AnnualRatePercentage / 100m,
            RateType: rateType,
            Frequency: frequency,
            Installments: installments,
            Method: method));

        return Task.FromResult(result.IsSuccess
            ? Result.Success(ToResponse(result.Value))
            : Result.Failure<SimulateScheduleResponse>(result.Error));
    }

    private static Error? Validate(
        SimulateScheduleRequest request,
        out int installments,
        out AmortizationMethod method,
        out RateType rateType,
        out PaymentFrequency frequency)
    {
        installments = request.Installments ?? request.TermMonths;
        method = default;
        rateType = default;
        frequency = default;

        if (request.Amount <= 0)
            return Error.Validation("Products.InvalidAmount", "Loan amount must be greater than zero.");

        if (request.Amount > MaxAmount)
            return Error.Validation("Products.InvalidAmount", $"Loan amount cannot exceed {MaxAmount:N0} in a simulation.");

        if (installments <= 0)
            return Error.Validation("Products.InvalidTerm", "Number of installments must be greater than zero.");

        if (installments > MaxInstallments)
            return Error.Validation("Products.InvalidTerm", $"Number of installments cannot exceed {MaxInstallments}.");

        if (request.AnnualRatePercentage < 0)
            return Error.Validation("Products.InvalidRate", "Annual rate percentage cannot be negative.");

        if (!AmortizationMethodParser.TryParse(request.Method, out method))
            return Error.Validation("Products.InvalidMethod", $"Amortization method '{request.Method}' is not supported. Supported: {AmortizationMethodParser.Supported}.");

        if (!RateTypeParser.TryParse(request.RateType, out rateType))
            return Error.Validation("Products.InvalidRateType", $"Rate type '{request.RateType}' is not supported. Supported: EFECTIVA, NOMINAL.");

        if (!PaymentFrequencyParser.TryParse(request.Frequency, out frequency))
            return Error.Validation("Products.InvalidFrequency", $"Frequency '{request.Frequency}' is not supported. Supported: {PaymentFrequencyParser.Supported}.");

        return null;
    }

    private static SimulateScheduleResponse ToResponse(ScheduleResult schedule) =>
        new(
            Amount: schedule.Principal.Amount,
            TotalInterest: schedule.TotalInterest.Amount,
            TotalPayment: schedule.TotalPayable.Amount,
            Installments: schedule.Lines
                .Select(l => new InstallmentSimulation(
                    Number: l.Number,
                    Principal: l.Principal.Amount,
                    Interest: l.Interest.Amount,
                    Total: l.Total.Amount,
                    RemainingBalance: l.RemainingBalance.Amount))
                .ToList(),
            Method: schedule.Method.ToString(),
            Frequency: schedule.Frequency.ToString(),
            PeriodRatePercentage: schedule.ReferencePeriodRate * 100m,
            InstallmentAmount: schedule.InstallmentAmount.Amount,
            EffectiveTeaPercentage: schedule.EffectiveAnnualRate is { } ear
                ? Math.Round(ear * 100m, 2, MidpointRounding.AwayFromZero)
                : null);
}
