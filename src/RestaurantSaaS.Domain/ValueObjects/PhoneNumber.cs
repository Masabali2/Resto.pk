using System.Text.RegularExpressions;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.ValueObjects;

/// <summary>
/// Normalized phone number (E.164-ish digits with optional leading '+').
/// Used for Restaurant contact, StaffMembership reachability and guest
/// order snapshots. Kept as a stored string on entities via conversion.
/// </summary>
public sealed class PhoneNumber : IEquatable<PhoneNumber>
{
    private static readonly Regex Pattern = new(
        @"^\+?[0-9\s\-()]{7,20}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int MaxStoredLength = 30;

    /// <summary>Digits only, with optional leading '+'.</summary>
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("PHONE_REQUIRED", "A phone number is required.");

        if (!Pattern.IsMatch(raw.Trim()))
            throw new DomainException("PHONE_INVALID", "Phone number format is not valid.");

        var plus = raw.Trim().StartsWith('+', StringComparison.Ordinal);
        var digits = new string(raw.Where(char.IsDigit).ToArray());

        if (digits.Length is < 7 or > 15) // E.164 limit
            throw new DomainException("PHONE_INVALID", "Phone number must contain 7 to 15 digits.");

        return new PhoneNumber((plus ? "+" : string.Empty) + digits);
    }

    public static bool TryParse(string? raw, out PhoneNumber? phone)
    {
        try
        {
            phone = Parse(raw);
            return true;
        }
        catch (DomainException)
        {
            phone = null;
            return false;
        }
    }

    public bool Equals(PhoneNumber? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is PhoneNumber p && Equals(p);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;
}
