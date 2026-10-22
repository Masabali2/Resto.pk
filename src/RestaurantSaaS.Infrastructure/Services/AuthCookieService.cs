
using Microsoft.AspNetCore.Http;

namespace RestaurantSaaS.Api.Services.Authentication;

public sealed class AuthCookieService(
    IHttpContextAccessor httpContextAccessor) : IAuthCookieService
{
    private const string AccessTokenCookie = "resto_access_token";
    private const string RefreshTokenCookie = "resto_refresh_token";

    public void SetAuthenticationCookies(
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        var response = GetResponse();

        response.Cookies.Append(
            AccessTokenCookie,
            accessToken,
            CreateCookieOptions(accessTokenExpiresAtUtc));

        response.Cookies.Append(
            RefreshTokenCookie,
            refreshToken,
            CreateCookieOptions(refreshTokenExpiresAtUtc));
    }

    public void SetRefreshTokenCookie(
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        var response = GetResponse();

        response.Cookies.Append(
            RefreshTokenCookie,
            refreshToken,
            CreateCookieOptions(refreshTokenExpiresAtUtc));
    }

    public bool TryReadRefreshToken(out string refreshToken)
    {
        var context = httpContextAccessor.HttpContext;

        if (context is not null &&
            context.Request.Cookies.TryGetValue(
                RefreshTokenCookie,
                out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            refreshToken = value;
            return true;
        }

        refreshToken = string.Empty;
        return false;
    }

    public void ClearAuthenticationCookies()
    {
        var response = GetResponse();

        response.Cookies.Delete(
            AccessTokenCookie,
            CreateDeletionCookieOptions());

        response.Cookies.Delete(
            RefreshTokenCookie,
            CreateDeletionCookieOptions());
    }

    private HttpResponse GetResponse()
    {
        return httpContextAccessor.HttpContext?.Response
            ?? throw new InvalidOperationException(
                "No active HTTP response is available.");
    }

    private static CookieOptions CreateCookieOptions(DateTime expiresAtUtc)
    {
        var maxAge = expiresAtUtc - DateTime.UtcNow;
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = new DateTimeOffset(
                DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
            MaxAge = maxAge > TimeSpan.Zero ? maxAge : TimeSpan.Zero
        };
    }

    private static CookieOptions CreateDeletionCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        };
    }
}