using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.ValueObjects;

/// <summary>
/// Strongly-typed monetary value object.
///
/// Why it exists (requirement #5/#42): money appears in MenuItem.Price,
/// Order totals and OrderItem lines. Using a primitive decimal everywhere
/// invites unit confusion and negative/absurd values crossing aggregate
/// boundaries. Money validates scale/precision at construction time and
/// provides safe arithmetic for server-side total calculation (the checkout
/// flow must NEVER trust client-computed amounts).
/// </summary>
public sealed class Money : IEquatable<Money>
{
    public const int MaxAmount = 1_000_000_000; // sanity bound for the decimal(18,4) column

    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = DefaultCurrency) => New(0m, currency);

    public const string DefaultCurrency = "PKR";

    public static Money New(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0m)
            throw new DomainException("MONEY_NEGATIVE", "Monetary amounts cannot be negative.");

        if (amount > MaxAmount)
            throw new DomainException("MONEY_TOO_LARGE", "Monetary amount exceeds the allowed maximum.");

        // Column is decimal(18,4); reject higher precision instead of silent rounding.
        if (decimal.Round(amount, 4) != amount)
            throw new DomainException("MONEY_PRECISION", "Monetary amounts support at most 4 decimal places.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("MONEY_CURRENCY", "Currency must be a 3-letter ISO code.");

        return new Money(amount, currency.ToUpperInvariant());
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return New(Amount + other.Amount, Currency);
    }

    /// <summary>Subtraction that refuses to produce negative money.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return New(Amount - other.Amount, Currency);
    }

    public Money Multiply(int factor)
    {
        if (factor < 0)
            throw new DomainException("MONEY_MULTIPLIER", "Factor cannot be negative.");
        return New(Amount * factor, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
            throw new DomainException("MONEY_CURRENCY_MISMATCH",
                $"Cannot combine {Currency} with {other.Currency}.");
    }

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount && Currency == other.Currency;

    public override bool Equals(object? obj) => obj is Money m && Equals(m);

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";

    public static bool operator ==(Money? left, Money? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Money? left, Money? right) => !(left == right);
}
