using LoanSystem.Modules.Products.Core.Application;
using LoanSystem.Modules.Products.Core.Domain;
using LoanSystem.Modules.Products.Core.Ports.Outbound;
using Shouldly;
using Xunit;

namespace LoanSystem.Application.UnitTests;

/// <summary>BE-PRO-06: the service loads holidays for the loan horizon and applies the configured weekdays.</summary>
public class DueDateServiceTests
{
    private sealed class FakeHolidayProvider(params DateOnly[] holidays) : IHolidayProvider
    {
        public List<(DateOnly From, DateOnly To)> Calls { get; } = [];

        public Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            Calls.Add((from, to));
            return Task.FromResult<IReadOnlySet<DateOnly>>(holidays.Where(d => d >= from && d <= to).ToHashSet());
        }
    }

    [Fact]
    public async Task GenerateAsync_LoadsHolidaysForTheHorizon_AndShiftsDueDates()
    {
        var provider = new FakeHolidayProvider(new DateOnly(2026, 12, 8));
        var service = new DueDateService(provider, new ProductsOptions());

        var result = await service.GenerateAsync(new DateOnly(2026, 11, 14), PaymentFrequency.MENSUAL, 12, 8, HolidayShift.NEXT);

        result.IsSuccess.ShouldBeTrue();
        result.Value[0].TheoreticalDueDate.ShouldBe(new DateOnly(2026, 12, 8));
        result.Value[0].DueDate.ShouldBe(new DateOnly(2026, 12, 9));
        provider.Calls.Single().From.ShouldBe(new DateOnly(2026, 11, 14));
        provider.Calls.Single().To.ShouldBeGreaterThanOrEqualTo(result.Value[^1].DueDate);
    }

    [Fact]
    public async Task GenerateAsync_UsesConfiguredNonBusinessWeekdays_AndMinDays()
    {
        var options = new ProductsOptions
        {
            Engine = new ProductsEngineOptions
            {
                NonBusinessWeekdays = [DayOfWeek.Saturday, DayOfWeek.Sunday],
                MinDaysToFirstInstallment = 30
            }
        };
        var service = new DueDateService(new FakeHolidayProvider(), options);

        // Payment day 12: 2026-12-12 is only 28 days away (< 30) → 2027-01-12 (Tuesday)
        var monthly = await service.GenerateAsync(new DateOnly(2026, 11, 14), PaymentFrequency.MENSUAL, 1, 12, HolidayShift.NEXT);
        monthly.Value[0].TheoreticalDueDate.ShouldBe(new DateOnly(2027, 1, 12));

        // Daily from Friday: Saturday is configured as non-business → Monday
        var daily = await service.GenerateAsync(new DateOnly(2026, 11, 13), PaymentFrequency.DIARIA, 1, null, HolidayShift.NEXT);
        daily.Value[0].DueDate.ShouldBe(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public async Task GenerateAsync_WithNoInstallments_IsInvalid()
    {
        var service = new DueDateService(new FakeHolidayProvider(), new ProductsOptions());

        var result = await service.GenerateAsync(new DateOnly(2026, 11, 14), PaymentFrequency.MENSUAL, 0, null, HolidayShift.NEXT);

        result.Error.Code.ShouldBe("Products.InvalidTerm");
    }
}
