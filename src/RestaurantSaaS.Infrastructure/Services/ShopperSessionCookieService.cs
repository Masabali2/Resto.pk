using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// HTTP cookie management for shopper sessions.
///
/// Follows the same conventions as AuthCookieService but uses a distinct
/// cookie name (resto_shopper_session) so staff authentication cookies are
/// never overwritten.
/// </summary>
public sealed class ShopperSessionCookieService(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ShopperSessionOptions> options) : IShopperSessionCookieService
{
    private const string CookieNameKey = "resto_shopper_session";

    public void SetSessionCookie(string rawToken, DateTime expiresAtUtc)
    {
        var response = GetResponse();
        var maxAge = expiresAtUtc - DateTime.UtcNow;
        response.Cookies.Append(
            CookieNameKey,
            rawToken,
            CreateCookieOptions(maxAge > TimeSpan.Zero ? maxAge : TimeSpan.Zero));
    }

    public string? ReadSessionToken()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null &&
            context.Request.Cookies.TryGetValue(CookieNameKey, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return null;
    }

    public void ClearSessionCookie()
    {
        var response = GetResponse();
        response.Cookies.Delete(CookieNameKey, CreateDeletionCookieOptions());
    }

    private HttpResponse GetResponse() =>
        httpContextAccessor.HttpContext?.Response
        ?? throw new InvalidOperationException("No active HTTP response is available.");

    private CookieOptions CreateCookieOptions(TimeSpan maxAge)
    {
        var opts = options.Value;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = opts.Secure,
            SameSite = ParseSameSite(opts.SameSite),
            Path = "/",
            Domain = opts.Domain,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.Add(maxAge), DateTimeKind.Utc)),
            MaxAge = maxAge > TimeSpan.Zero ? maxAge : TimeSpan.Zero
        };
    }

    private static CookieOptions CreateDeletionCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
    }

    private static SameSiteMode ParseSameSite(string sameSite) =>
        sameSite switch
        {
            "Strict" => SameSiteMode.Strict,
            "None" => SameSiteMode.None,
            _ => SameSiteMode.Lax
        };
}
