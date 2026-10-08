using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.QRCodes;

/// <summary>
/// A scannable QR code pointing customers at a restaurant's public ordering
/// page. Code is globally UNIQUE and opaque (random) — it is a bearer-style
/// lookup key, so it must not be guessable. Deactivating a code stops routing
/// without deleting history.
/// Composite FK (TenantId, RestaurantId) enforces tenant/restaurant coherence.
/// </summary>
public class QRCode : AuditableEntity, ITenantOwned
{
    public const int CodeLength = 16;

    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Short opaque routing code embedded in the URL. Unique across the platform.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Resolved target (website slug URL) captured at generation time.</summary>
    public string TargetUrl { get; private set; } = null!;

    public bool IsActive { get; private set; } = true;

    public Restaurant Restaurant { get; private set; } = null!;

    public static QRCode Create(Guid tenantId, Guid restaurantId, string name, string code, string targetUrl)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("QR_TENANT_REQUIRED", "A QR code must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException("QR_RESTAURANT_REQUIRED", "A QR code must belong to a restaurant.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("QR_NAME_REQUIRED", "QR code name is required.");

        if (string.IsNullOrWhiteSpace(code) || code.Length < CodeLength)
            throw new DomainException("QR_CODE_INVALID", $"QR routing code must be at least {CodeLength} characters.");

        if (string.IsNullOrWhiteSpace(targetUrl) || !targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("QR_TARGET_INVALID", "QR target must be an absolute URL.");

        return new QRCode
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            Name = name.Trim(),
            Code = code.Trim(),
            TargetUrl = targetUrl.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Generates an unguessable routing code (cryptographic RNG).</summary>
    public static string GenerateCode()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // Crockford-ish, no confusable chars
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(CodeLength);
        return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("QR_NAME_REQUIRED", "QR code name is required.");

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }
}
