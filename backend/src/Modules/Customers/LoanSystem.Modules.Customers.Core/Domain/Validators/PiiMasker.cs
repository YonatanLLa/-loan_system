namespace LoanSystem.Modules.Customers.Core.Domain.Validators;

/// <summary>RNF-05 PII masking helpers for API responses and safe diagnostic logging.</summary>
internal static class PiiMasker
{
    public static string MaskDocNumber(string? docNumber) => MaskTrailing(docNumber);

    public static string? MaskPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : MaskTrailing(phone);

    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex == trimmed.Length - 1)
        {
            return "***";
        }

        return $"{trimmed[0]}***{trimmed[atIndex..]}";
    }

    public static string MaskTarget(OtpChannel channel, string target) =>
        channel == OtpChannel.EMAIL
            ? MaskEmail(target) ?? "***"
            : MaskTrailing(target);

    private static string MaskTrailing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "****";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return string.Concat(new string('*', trimmed.Length - 4), trimmed[^4..]);
    }
}
