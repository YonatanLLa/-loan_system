using System.Net.Mail;
using System.Text.RegularExpressions;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed partial class ContactInfo : ValueObject
{
    public const int MaxEmailLength = 150;

    public string Email { get; }
    public string PhonePrimary { get; }
    public string? PhoneSecondary { get; }

    private ContactInfo(string email, string phonePrimary, string? phoneSecondary)
    {
        Email = email;
        PhonePrimary = phonePrimary;
        PhoneSecondary = phoneSecondary;
    }

    public static Result<ContactInfo> Create(string? rawEmail, string? rawPhonePrimary, string? rawPhoneSecondary)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return CustomerErrors.Validation("Email is required.");
        }

        var email = rawEmail.Trim().ToLowerInvariant();
        if (email.Length > MaxEmailLength || !IsValidEmail(email))
        {
            return CustomerErrors.Validation("Email format is invalid.");
        }

        var primary = NormalizePeruvianMobile(rawPhonePrimary);
        if (primary is null)
        {
            return CustomerErrors.Validation("Primary phone must be a 9-digit Peruvian mobile number starting with 9.");
        }

        string? secondary = null;
        if (!string.IsNullOrWhiteSpace(rawPhoneSecondary))
        {
            secondary = NormalizePeruvianMobile(rawPhoneSecondary);
            if (secondary is null)
            {
                return CustomerErrors.Validation("Secondary phone must be a 9-digit Peruvian mobile number starting with 9.");
            }
        }

        return new ContactInfo(email, primary, secondary);
    }

    public static string? NormalizePeruvianMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var compact = NonDigitExceptPlusRegex().Replace(raw.Trim(), string.Empty);
        if (compact.StartsWith("+51", StringComparison.Ordinal))
        {
            compact = compact[3..];
        }
        else if (compact.StartsWith("51", StringComparison.Ordinal) && compact.Length == 11)
        {
            compact = compact[2..];
        }

        return PeruvianMobileRegex().IsMatch(compact) ? compact : null;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(email);
            if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var atIndex = email.IndexOf('@');
            return atIndex > 0 && email.IndexOf('.', atIndex) > atIndex + 1 && !email.EndsWith('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return PhonePrimary;
        yield return PhoneSecondary;
    }

    [GeneratedRegex(@"[\s\-()]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonDigitExceptPlusRegex();

    [GeneratedRegex(@"^9\d{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex PeruvianMobileRegex();
}
