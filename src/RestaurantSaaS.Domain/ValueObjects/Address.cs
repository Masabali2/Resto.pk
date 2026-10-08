using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.ValueObjects;

/// <summary>
/// Postal address value object used by Restaurant.
/// Justified because the restaurant address is validated as a unit
/// (line required, bounded lengths) and queried together (city/country filters).
/// Persisted as owned columns via EF Core shadow-owned entity.
/// </summary>
public sealed record Address
{
    public const int LineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int CountryMaxLength = 100;
    public const int PostalCodeMaxLength = 20;

    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string Country { get; }
    public string? PostalCode { get; }

    private Address(string line1, string? line2, string city, string country, string? postalCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        Country = country;
        PostalCode = postalCode;
    }

    public static Address Create(
        string line1,
        string city,
        string country,
        string? line2 = null,
        string? postalCode = null)
    {
        if (string.IsNullOrWhiteSpace(line1))
            throw new DomainException("ADDRESS_LINE_REQUIRED", "Address line 1 is required.");

        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("ADDRESS_CITY_REQUIRED", "City is required.");

        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("ADDRESS_COUNTRY_REQUIRED", "Country is required.");

        if (line1.Length > LineMaxLength || city.Length > CityMaxLength ||
            country.Length > CountryMaxLength ||
            (line2?.Length > LineMaxLength) || (postalCode?.Length > PostalCodeMaxLength))
            throw new DomainException("ADDRESS_TOO_LONG", "Address component exceeds the maximum length.");

        return new Address(
            line1.Trim(),
            line2?.Trim(),
            city.Trim(),
            country.Trim(),
            postalCode?.Trim());
    }
}
