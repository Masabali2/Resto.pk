namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Exposes the validated shopper session hash to Application services during
/// a request. Implemented over HttpContext.Items so Domain/Application layers
/// never take a direct HttpContext dependency.
/// </summary>
public interface IShopperSessionContext
{
    /// <summary>The SHA-256 hash of the validated session token, or null if no valid session exists.</summary>
    string? SessionHash { get; set; }

    /// <summary>Whether a valid shopper session is active for this request.</summary>
    bool HasSession { get; }
}
