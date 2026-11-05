using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Api.Middleware;

/// <summary>
/// Validates the Origin header on cookie-authenticated state-changing requests.
///
/// This prevents cross-site request forgery when shopper session cookies are
/// sent automatically by the browser. Only origins listed in
/// ShopperSessionOptions.AllowedOrigins may perform cookie-authenticated
/// mutations. Public GET endpoints are never blocked.
///
/// The Origin check is a defense-in-depth layer alongside ASP.NET Core
/// antiforgery tokens (X-XSRF-TOKEN).
/// </summary>
public sealed class OriginValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<ShopperSessionOptions> options)
    {
        if (!SafeMethods.Contains(context.Request.Method) &&
            context.Request.Cookies.ContainsKey("resto_shopper_session"))
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (string.IsNullOrEmpty(origin) ||
                !options.Value.AllowedOrigins.Contains(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"success\":false,\"errorCode\":\"ORIGIN_NOT_ALLOWED\",\"message\":\"Origin is not allowed.\"}");
                return;
            }
        }

        await next(context);
    }
}
