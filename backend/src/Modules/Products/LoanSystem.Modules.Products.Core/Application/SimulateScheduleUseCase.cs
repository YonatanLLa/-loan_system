using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;
using LoanSystem.Modules.Products.Core.Contracts;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Ports.Inbound;

namespace LoanSystem.Modules.Products.Core.Application;

internal sealed class SimulateScheduleUseCase : ISimulateScheduleUseCase
{
    public Task<Result<SimulateScheduleResponse>> ExecuteAsync(SimulateScheduleRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request, out var method, out var rateType);
        if (validation is not null)
            return Task.FromResult<Result<SimulateScheduleResponse>>(validation);

        var schedule = method switch
        {
            AmortizationMethod.French => FrenchAmortizationSchedule.Build(
                Money.From(request.Amount), request.AnnualRatePercentage, request.TermMonths, rateType),
            _ => throw new InvalidOperationException($"Unsupported amortization method '{method}'.")
        };

        return Task.FromResult(Result.Success(ToResponse(schedule)));
    }

    private static Error? Validate(SimulateScheduleRequest request, out AmortizationMethod method, out RateType rateType)
    {
        method = default;
        rateType = default;

        if (request.Amount <= 0)
            return Error.Validation("Products.InvalidAmount", "Loan amount must be greater than zero.");

        if (request.TermMonths <= 0)
            return Error.Validation("Products.InvalidTerm", "Term in months must be greater than zero.");

        if (request.AnnualRatePercentage < 0)
            return Error.Validation("Products.InvalidRate", "Annual rate percentage cannot be negative.");

        if (!AmortizationMethodParser.TryParse(request.Method, out method))
            return Error.Validation("Products.InvalidMethod", $"Amortization method '{request.Method}' is not supported. Supported: FRENCH.");

        if (!RateTypeParser.TryParse(request.RateType, out rateType))
            return Error.Validation("Products.InvalidRateType", $"Rate type '{request.RateType}' is not supported. Supported: EFECTIVA, NOMINAL.");

        return null;
    }

    private static SimulateScheduleResponse ToResponse(AmortizationSchedule schedule) =>
        new(
            Amount: schedule.Principal.Amount,
            TotalInterest: schedule.TotalInterest.Amount,
            TotalPayment: schedule.TotalPayment.Amount,
            Installments: schedule.Installments
                .Select(i => new InstallmentSimulation(
                    Number: i.Number,
                    Principal: i.Principal.Amount,
                    Interest: i.Interest.Amount,
                    Total: i.Total.Amount,
                    RemainingBalance: i.RemainingBalance.Amount))
                .ToList());
}
