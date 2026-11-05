using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Api.Middleware;

/// <summary>
/// Manages anonymous shopper sessions for public customer ordering.
///
/// On each request:
///  - Reads the resto_shopper_session cookie.
///  - If valid, exposes its SHA-256 hash via IShopperSessionContext.
///  - If missing/invalid/expired, creates a new session, sets the cookie,
///    and exposes the new hash.
///
/// Session identity is never rotated on every request — a valid existing
/// session is preserved. The raw token never leaves the middleware.
/// </summary>
public sealed class ShopperSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IShopperSessionService sessionService,
        IShopperSessionCookieService cookieService,
        IShopperSessionContext sessionContext,
        IOptions<ShopperSessionOptions> options)
    {
        var rawToken = cookieService.ReadSessionToken();
        string? sessionHash = null;

        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            sessionHash = sessionService.ValidateAndGetHash(rawToken);
        }

        // Missing, malformed, or expired → issue a replacement session.
        if (sessionHash is null)
        {
            var (newRawToken, newHash) = sessionService.CreateSession();
            sessionHash = newHash;

            var lifetimeHours = options.Value.LifetimeHours;
            var expiresAt = DateTime.UtcNow.AddHours(lifetimeHours);
            cookieService.SetSessionCookie(newRawToken, expiresAt);
        }

        sessionContext.SessionHash = sessionHash;

        await next(context);
    }
}
