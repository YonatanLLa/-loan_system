namespace LoanSystem.Common.Core.Domain;

public sealed class Money : ValueObject, IComparable<Money>
{
    public const string DefaultCurrency = "PEN";
    public const int DecimalPlaces = 2;

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        Currency = currency.Trim().ToUpperInvariant();
        Amount = Round(amount);
    }

    public static decimal Round(decimal value) =>
        Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money From(decimal amount, string currency = DefaultCurrency) => new(amount, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public Money Divide(decimal divisor)
    {
        if (divisor == 0) throw new DivideByZeroException("Cannot divide Money by zero.");
        return new Money(Amount / divisor, Currency);
    }

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);
    public static Money operator *(Money a, decimal factor) => a.Multiply(factor);
    public static Money operator *(decimal factor, Money a) => a.Multiply(factor);
    public static Money operator /(Money a, decimal divisor) => a.Divide(divisor);

    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;
    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{Currency} {Amount:N2}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot operate on money with different currencies: '{Currency}' and '{other.Currency}'.");
    }
}
