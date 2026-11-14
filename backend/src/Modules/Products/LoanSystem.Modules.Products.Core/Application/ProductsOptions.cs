namespace LoanSystem.Modules.Products.Core.Application;

/// <summary>Configuration section <c>Products</c> (PLAN_PRODUCTOS §11). Sections are added as each requirement lands.</summary>
internal sealed class ProductsOptions
{
    public const string SectionName = "Products";

    public ProductsEngineOptions Engine { get; init; } = new();
    public ProductsCacheOptions Cache { get; init; } = new();
}

internal sealed class ProductsEngineOptions
{
    public int MaxInstallments { get; init; } = 720;
    public decimal MaxSimulationAmount { get; init; } = 10_000_000m;

    /// <summary>With a fixed payment day, minimum days from disbursement to the first due date (PRO-06.6).</summary>
    public int MinDaysToFirstInstallment { get; init; } = 15;

    /// <summary>
    /// Weekdays that are never business days (plan D5). <c>null</c> = Sunday only. Kept nullable because the
    /// configuration binder appends to (instead of replacing) a pre-filled array.
    /// </summary>
    public DayOfWeek[]? NonBusinessWeekdays { get; init; }

    public IReadOnlySet<DayOfWeek> ResolvedNonBusinessWeekdays =>
        NonBusinessWeekdays is null ? new HashSet<DayOfWeek> { DayOfWeek.Sunday } : new HashSet<DayOfWeek>(NonBusinessWeekdays);
}

internal sealed class ProductsCacheOptions
{
    public int ProductVersionMinutes { get; init; } = 5;
    public int HolidaysMinutes { get; init; } = 60;
}
