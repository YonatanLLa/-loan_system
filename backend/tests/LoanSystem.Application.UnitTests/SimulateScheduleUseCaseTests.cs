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
    public async Task ExecuteAsync_DefaultRateType_ShouldTreatRateAsTea()
    {
        // Default RateType = EFECTIVA: TEA 12% => monthly 0.9489% => 885.62
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(10000m, 12m, 12, "french"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments[0].Total.ShouldBe(885.62m);
    }

    [Fact]
    public async Task ExecuteAsync_WithNominalRateType_ShouldUseTnaDividedBy12()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(10000m, 12m, 12, "FRENCH", "NOMINAL"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments[0].Total.ShouldBe(888.49m);
    }

    [Theory] // PRO-02.8: new methods, PG labels and the FRENCH alias (§12 V1–V4)
    [InlineData("FRANCES", 957.86, null)]
    [InlineData("FRENCH", 957.86, null)]
    [InlineData("aleman", 1054.37, null)]
    [InlineData("AMERICANO", 221.04, null)]
    [InlineData("FLAT", 1054.38, 56.80)]
    public async Task ExecuteAsync_WithEachMethod_ShouldReturnReferenceInstallment(string method, decimal firstInstallment, double? effectiveTea)
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(10000m, 30m, 12, method));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments[0].Total.ShouldBe(firstInstallment);
        result.Value.InstallmentAmount.ShouldBe(firstInstallment);
        result.Value.Installments.Sum(i => i.Principal).ShouldBe(10000m);
        result.Value.EffectiveTeaPercentage.ShouldBe(effectiveTea is null ? null : (decimal)effectiveTea.Value);
    }

    [Fact] // V9: installments + frequency, termMonths omitted
    public async Task ExecuteAsync_WithWeeklyFrequencyAndInstallments_ShouldUseSevenDayPeriods()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(
            10000m, 30m, Method: "FRANCES", Frequency: "SEMANAL", Installments: 52));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments.Count.ShouldBe(52);
        result.Value.Installments[0].Total.ShouldBe(219.50m);
        result.Value.Frequency.ShouldBe("SEMANAL");
        result.Value.Method.ShouldBe("FRANCES");
    }

    [Fact]
    public async Task ExecuteAsync_InstallmentsWinsOverTermMonths()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(10000m, 30m, 12, Installments: 6));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installments.Count.ShouldBe(6);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownFrequency_ShouldReturnValidationError()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(1000m, 10m, 12, Frequency: "ANUAL"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Products.InvalidFrequency");
    }

    [Theory] // PRO-02.8 hard limits
    [InlineData(10_000_000.01, 12, "Products.InvalidAmount")]
    [InlineData(1000, 721, "Products.InvalidTerm")]
    public async Task ExecuteAsync_AboveHardLimits_ShouldReturnValidationError(decimal amount, int installments, string code)
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(amount, 10m, installments));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownRateType_ShouldReturnValidationError()
    {
        var result = await _useCase.ExecuteAsync(new SimulateScheduleRequest(1000m, 10m, 12, "FRENCH", "EFFECTIVE"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Products.InvalidRateType");
    }
}
