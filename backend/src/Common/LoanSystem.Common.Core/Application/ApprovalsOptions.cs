namespace LoanSystem.Common.Core.Application;

/// <summary>
/// Shared configuration for the four-eyes approval engine (BE-SEC-09).
/// Expiration lifetimes and step-up MFA rules come from configuration; permission rules stay in code.
/// </summary>
public sealed class ApprovalsOptions
{
    public int DefaultExpirationHours { get; init; } = 72;

    public IDictionary<string, int> ExpirationHours { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> MfaStepUpOperations { get; init; } = ["*"];

    public int WriteOffMinDaysLate { get; init; } = 180;

    public TimeSpan GetExpirationFor(string operationType)
    {
        var hours = ExpirationHours.TryGetValue(operationType, out var configured) && configured > 0
            ? configured
            : DefaultExpirationHours;

        if (hours <= 0)
        {
            hours = 72;
        }

        return new TimeSpan(hours, 0, 0);
    }

    public bool RequiresMfaStepUp(string operationType) =>
        MfaStepUpOperations.Any(op =>
            string.Equals(op, "*", StringComparison.Ordinal)
            || string.Equals(op, operationType, StringComparison.OrdinalIgnoreCase));
}
