using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LoanSystem.Common.Core.Application;
using LoanSystem.Common.Core.Domain;

namespace LoanSystem.Modules.Customers.Core.Domain;

internal sealed partial class PersonName : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 200;

    public string Value { get; }

    private PersonName(string value) => Value = value;

    public static Result<PersonName> Create(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return CustomerErrors.Validation("Full name is required.");
        }

        var normalized = CollapseWhitespaceRegex().Replace(rawName.Trim(), " ").ToUpperInvariant();
        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            return CustomerErrors.Validation($"Full name must be between {MinLength} and {MaxLength} characters.");
        }

        return new PersonName(normalized);
    }

    /// <summary>
    /// Normalizes names for titularity matching (uppercase, collapsed spaces, diacritics removed; CUS-09.6).
    /// </summary>
    public static string NormalizeForComparison(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        var decomposed = rawName.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var asciiLike = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        return CollapseWhitespaceRegex().Replace(asciiLike.Trim(), " ");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex CollapseWhitespaceRegex();
}
