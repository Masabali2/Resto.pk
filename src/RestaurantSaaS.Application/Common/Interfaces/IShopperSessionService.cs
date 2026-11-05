namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Backend-managed anonymous shopper session infrastructure.
///
/// Sessions are valid independently of whether a cart exists. The raw token
/// is never returned to the caller — only the SHA-256 hash is exposed, suitable
/// for storage in Cart.SessionId.
/// </summary>
public interface IShopperSessionService
{
    /// <summary>
    /// Creates a new shopper session token. Returns the raw token (to be set
    /// in a cookie) and its SHA-256 hash (to be stored in the database).
    /// </summary>
    (string RawToken, string Hash) CreateSession();

    /// <summary>
    /// Validates a raw shopper session token and returns its SHA-256 hash if
    /// valid. Returns null if the token is missing, malformed, expired, or
    /// has an invalid signature.
    /// </summary>
    string? ValidateAndGetHash(string? rawToken);
}
