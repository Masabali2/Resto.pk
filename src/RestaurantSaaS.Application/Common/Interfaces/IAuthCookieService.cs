
namespace RestaurantSaaS.Api.Services.Authentication;

public interface IAuthCookieService
{
    void SetAuthenticationCookies(
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc);

    void ClearAuthenticationCookies();
}