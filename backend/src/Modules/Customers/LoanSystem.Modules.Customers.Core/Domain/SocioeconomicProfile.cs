using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed class SocioeconomicProfile : ValueObject
{
    public const int MaxSeniorityMonths = 600;
    public const int MaxOccupationLength = 150;
    public const int MaxEmployerLength = 200;

    public Money MonthlyNetIncome { get; }
    public string? Occupation { get; }
    public string? EmployerName { get; }
    public int WorkSeniorityMonths { get; }

    private SocioeconomicProfile(
        Money monthlyNetIncome,
        string? occupation,
        string? employerName,
        int workSeniorityMonths)
    {
        MonthlyNetIncome = monthlyNetIncome;
        Occupation = occupation;
        EmployerName = employerName;
        WorkSeniorityMonths = workSeniorityMonths;
    }

    public static Result<SocioeconomicProfile> Create(
        decimal monthlyNetIncome,
        string? occupation,
        string? employerName,
        int workSeniorityMonths)
    {
        if (monthlyNetIncome < 0m)
        {
            return CustomerErrors.Validation("Monthly net income cannot be negative.");
        }

        if (workSeniorityMonths < 0 || workSeniorityMonths > MaxSeniorityMonths)
        {
            return CustomerErrors.Validation($"Work seniority months must be between 0 and {MaxSeniorityMonths}.");
        }

        var normalizedOccupation = string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim();
        if (normalizedOccupation is { Length: > MaxOccupationLength })
        {
            return CustomerErrors.Validation($"Occupation must not exceed {MaxOccupationLength} characters.");
        }

        var normalizedEmployer = string.IsNullOrWhiteSpace(employerName) ? null : employerName.Trim();
        if (normalizedEmployer is { Length: > MaxEmployerLength })
        {
            return CustomerErrors.Validation($"Employer name must not exceed {MaxEmployerLength} characters.");
        }

        return new SocioeconomicProfile(
            Money.From(monthlyNetIncome, Money.DefaultCurrency),
            normalizedOccupation,
            normalizedEmployer,
            workSeniorityMonths);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MonthlyNetIncome;
        yield return Occupation;
        yield return EmployerName;
        yield return WorkSeniorityMonths;
    }
}
