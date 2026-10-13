
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Validation;

public static class SlugValidator
{
    public static string Normalize(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("TENANT_SLUG_REQUIRED", "Tenant slug is required.");

        var normalized = new string(
            slug.Trim()
                .ToLowerInvariant()
                .Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-')
                .ToArray());

        normalized = string.Join(
            '-',
            normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length is < 3 or > 63)
            throw new DomainException("TENANT_SLUG_INVALID", "Tenant slug must contain 3 to 63 characters.");

        return normalized;
    }
}