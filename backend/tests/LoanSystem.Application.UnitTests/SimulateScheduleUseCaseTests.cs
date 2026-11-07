using LoanSystem.Common.Core.Application;
using LoanSystem.Modules.Products.Core.Application;
using LoanSystem.Modules.Products.Core.Contracts;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

public class SimulateScheduleUseCaseTests
{
    private readonly SimulateScheduleUseCase _useCase = new();

    [Fact]
    public async Task ExecuteAsync_WithValidInputs_ShouldReturnAccurateSchedule()
    {
        var request = new SimulateScheduleRequest(
            Amount: 10000m,
            AnnualRatePercentage: 12m,
            TermMonths: 12,
            Method: "FRENCH");

        var result = await _useCase.ExecuteAsync(request);

        result.IsSuccess.ShouldBeTrue();
        var schedule = result.Value;

        schedule.Amount.ShouldBe(10000m);
        schedule.Installments.Count.ShouldBe(12);

        // Sum of all installments principal MUST exactly match total loan principal (centavo adjustment rule)
        decimal totalAmortizedPrincipal = schedule.Installments.Sum(i => i.Principal);
        totalAmortizedPrincipal.ShouldBe(10000m);

        // Remaining balance of last installment must be 0
        schedule.Installments.Last().RemainingBalance.ShouldBe(0m);

        // Total payment must equal principal + total interest
        schedule.TotalPayment.ShouldBe(schedule.Amount + schedule.TotalInterest);
        schedule.TotalInterest.ShouldBeGreaterThan(0m);
    }

    [Theory]
    [InlineData(0, 10, 12)]
    [InlineData(-500, 10, 12)]
    public async Task ExecuteAsync_WithInvalidAmount_ShouldReturnValidationError(decimal amount, decimal rate, int term)
    {
        var request = new SimulateScheduleRequest(amount, rate, term);

        var result = await _useCase.ExecuteAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Products.InvalidAmount");
    }

    [Theory]
    [InlineData(1000, 10, 0)]
    [InlineData(1000, 10, -3)]
    public async Task ExecuteAsync_WithInvalidTerm_ShouldReturnValidationError(decimal amount, decimal rate, int term)
    {
        var request = new SimulateScheduleRequest(amount, rate, term);

        var result = await _useCase.ExecuteAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Products.InvalidTerm");
    }

    [Theory]
    [InlineData("GERMAN")]
    [InlineData("")]
    public async Task ExecuteAsync_WithUnsupportedMethod_ShouldReturnValidationError(string method)
    {
        var request = new SimulateScheduleRequest(1000m, 10m, 12, method);

        var result = await _useCase.ExecuteAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Products.InvalidMethod");
    }

    [Fact]
    public async Task ExecuteAsync_WithFrenchMethod_ShouldReturnStandardQuota()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(10000m, 12m, 12, "french"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments[0].Total.ShouldBe(888.49m);
    }
}
