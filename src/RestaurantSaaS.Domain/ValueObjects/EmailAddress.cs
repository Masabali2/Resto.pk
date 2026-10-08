using System.Text.RegularExpressions;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.ValueObjects;

/// <summary>
/// Normalized e-mail address.
///
/// SCOPE NOTE: ASP.NET Identity owns the canonical user credential email
/// (AspNetUsers.Email). This value object is used ONLY for non-Identity
/// contact data (Restaurant contact email, Customer/Cart snapshot emails)
/// so we do not create a second competing identity store (requirement #6).
/// </summary>
public sealed class EmailAddress : IEquatable<EmailAddress>
{
    // Deliberately pragmatic RFC-5322-lite pattern; deliverability is proven by verification, not regex.
    private static readonly Regex Pattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int MaxLength = 256;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DomainException("EMAIL_REQUIRED", "An e-mail address is required.");

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            throw new DomainException("EMAIL_TOO_LONG", "E-mail address is too long.");

        if (!Pattern.IsMatch(normalized))
            throw new DomainException("EMAIL_INVALID", "E-mail address is not valid.");

        return new EmailAddress(normalized);
    }

    public static bool TryParse(string? raw, out EmailAddress? email)
    {
        try
        {
            email = Parse(raw);
            return true;
        }
        catch (DomainException)
        {
            email = null;
            return false;
        }
    }

    public bool Equals(EmailAddress? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is EmailAddress e && Equals(e);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Value;
}
