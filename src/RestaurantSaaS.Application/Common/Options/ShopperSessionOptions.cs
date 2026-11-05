using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Common.Options;

/// <summary>
/// Shopper session configuration. Secret is loaded from configuration/user-secrets
/// in development and a secure secret store in production — it is NEVER committed
/// to source control. This key is distinct from the JWT staff signing key.
/// </summary>
public sealed class ShopperSessionOptions : IValidatableObject
{
    public const string SectionName = "ShopperSession";

    /// <summary>Cookie name for the shopper session token.</summary>
    public string CookieName { get; init; } = "resto_shopper_session";

    /// <summary>Session lifetime in hours (fixed, no sliding).</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 720)]
    public int LifetimeHours { get; init; } = 24;

    /// <summary>Signing key for HMAC-SHA256 token integrity (min 32 chars).</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(32)]
    public string Secret { get; init; } = null!;

    /// <summary>SameSite policy: Lax, Strict, or None.</summary>
    public string SameSite { get; init; } = "Lax";

    /// <summary>Whether the cookie requires HTTPS (Secure flag).</summary>
    public bool Secure { get; init; } = true;

    /// <summary>Cookie domain (null for host-only).</summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Allowed frontend origins for cookie-authenticated mutations (Origin
    /// validation defense-in-depth). Empty = no origins allowed.
    /// </summary>
    public ISet<string> AllowedOrigins { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        System.ComponentModel.DataAnnotations.ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(Secret))
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "ShopperSession:Secret is required.");
            yield break;
        }

        if (Secret.Length < 32)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                "ShopperSession:Secret must be at least 32 characters.");
        }

        if (!System.Enum.TryParse<Microsoft.AspNetCore.Http.SameSiteMode>(
                SameSite, ignoreCase: true, out var _sameSite))
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"ShopperSession:SameSite '{SameSite}' is invalid. Use Lax, Strict, or None.");
        }
    }
}
