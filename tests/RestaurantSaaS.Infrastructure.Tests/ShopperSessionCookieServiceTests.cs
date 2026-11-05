using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class ShopperSessionCookieServiceTests
{
    private static ShopperSessionOptions CreateOptions() =>
        new()
        {
            CookieName = "resto_shopper_session",
            LifetimeHours = 24,
            Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SameSite = "Lax",
            Secure = true,
            Domain = null,
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };

    private static (ShopperSessionCookieService service, DefaultHttpContext httpContext) CreateService(
        ShopperSessionOptions? options = null)
    {
        var opts = options ?? CreateOptions();
        var httpContext = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        var service = new ShopperSessionCookieService(accessor.Object, Options.Create(opts));
        return (service, httpContext);
    }

    [Fact]
    public void SetSessionCookie_SetsHttpOnlyTrue()
    {
        var (service, httpContext) = CreateService();

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetSessionCookie_SetsSecureTrue()
    {
        var (service, httpContext) = CreateService();

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetSessionCookie_SetsSameSiteLax()
    {
        var (service, httpContext) = CreateService();

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetSessionCookie_SetsPathRoot()
    {
        var (service, httpContext) = CreateService();

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("path=/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetSessionCookie_UsesConfiguredCookieName()
    {
        var (service, httpContext) = CreateService();

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.StartsWith("resto_shopper_session="));
    }

    [Fact]
    public void SetSessionCookie_SecureFalse_WhenConfiguredSecureFalse()
    {
        var opts = new ShopperSessionOptions
        {
            CookieName = "resto_shopper_session",
            LifetimeHours = 24,
            Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SameSite = "Lax",
            Secure = false,
            Domain = null,
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };
        var (service, httpContext) = CreateService(opts);

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().NotContain(c => c.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetSessionCookie_SetsDomain_WhenConfigured()
    {
        var opts = new ShopperSessionOptions
        {
            CookieName = "resto_shopper_session",
            LifetimeHours = 24,
            Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SameSite = "Lax",
            Secure = true,
            Domain = "example.com",
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };
        var (service, httpContext) = CreateService(opts);

        service.SetSessionCookie("token-value", DateTime.UtcNow.AddHours(24));

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("domain=example.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadSessionToken_ReturnsCookieValue_WhenPresent()
    {
        var (service, httpContext) = CreateService();
        httpContext.Request.Headers.Cookie = "resto_shopper_session=token-value";

        var result = service.ReadSessionToken();

        result.Should().Be("token-value");
    }

    [Fact]
    public void ReadSessionToken_ReturnsNull_WhenCookieMissing()
    {
        var (service, _) = CreateService();

        var result = service.ReadSessionToken();

        result.Should().BeNull();
    }

    [Fact]
    public void ReadSessionToken_ReturnsNull_WhenCookieEmpty()
    {
        var (service, httpContext) = CreateService();
        httpContext.Request.Headers.Cookie = "resto_shopper_session=";

        var result = service.ReadSessionToken();

        result.Should().BeNull();
    }

    [Fact]
    public void ClearSessionCookie_DeletesCookie()
    {
        var (service, httpContext) = CreateService();

        service.ClearSessionCookie();

        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("resto_shopper_session="));
        httpContext.Response.Headers.SetCookie.Should().Contain(c => c.Contains("expires="));
    }
}
