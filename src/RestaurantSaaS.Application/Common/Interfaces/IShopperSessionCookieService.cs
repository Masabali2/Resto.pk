namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Manages the shopper session HTTP cookie. The raw token is never exposed
/// through this service — only set on the response or read from the request.
/// </summary>
public interface IShopperSessionCookieService
{
    /// <summary>Sets the shopper session cookie on the response.</summary>
    void SetSessionCookie(string rawToken, DateTime expiresAtUtc);

    /// <summary>Reads the shopper session token from the request cookie.</summary>
    string? ReadSessionToken();

    /// <summary>Clears the shopper session cookie.</summary>
    void ClearSessionCookie();
}
