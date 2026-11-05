using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class ShopperSessionMiddlewareTests
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

    private static (DefaultHttpContext context, Mock<IShopperSessionService> sessionService,
        Mock<IShopperSessionCookieService> cookieService, ShopperSessionContext sessionContext,
        IOptions<ShopperSessionOptions> options) CreateSetup()
    {
        var context = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);
        var sessionContext = new ShopperSessionContext(accessor.Object);
        var sessionService = new Mock<IShopperSessionService>();
        var cookieService = new Mock<IShopperSessionCookieService>();
        var options = Options.Create(CreateOptions());
        return (context, sessionService, cookieService, sessionContext, options);
    }

    [Fact]
    public async Task InvokeAsync_MissingToken_CreatesNewSessionAndSetsCookie()
    {
        var (context, sessionService, cookieService, sessionContext, options) = CreateSetup();
        cookieService.Setup(c => c.ReadSessionToken()).Returns((string?)null);
        sessionService.Setup(s => s.ValidateAndGetHash(null)).Returns((string?)null);
        sessionService.Setup(s => s.CreateSession()).Returns(("raw-token", "hash-value"));

        var middleware = new ShopperSessionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, sessionService.Object, cookieService.Object, sessionContext, options);

        sessionService.Verify(s => s.CreateSession(), Times.Once);
        cookieService.Verify(c => c.SetSessionCookie("raw-token", It.Is<DateTime>(d => d > DateTime.UtcNow)), Times.Once);
        sessionContext.SessionHash.Should().Be("hash-value");
        sessionContext.HasSession.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_PreservesSession_DoesNotCreateNew()
    {
        var (context, sessionService, cookieService, sessionContext, options) = CreateSetup();
        cookieService.Setup(c => c.ReadSessionToken()).Returns("valid-token");
        sessionService.Setup(s => s.ValidateAndGetHash("valid-token")).Returns("existing-hash");

        var middleware = new ShopperSessionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, sessionService.Object, cookieService.Object, sessionContext, options);

        sessionService.Verify(s => s.CreateSession(), Times.Never);
        cookieService.Verify(c => c.SetSessionCookie(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        sessionContext.SessionHash.Should().Be("existing-hash");
        sessionContext.HasSession.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_CreatesNewSession()
    {
        var (context, sessionService, cookieService, sessionContext, options) = CreateSetup();
        cookieService.Setup(c => c.ReadSessionToken()).Returns("tampered-token");
        sessionService.Setup(s => s.ValidateAndGetHash("tampered-token")).Returns((string?)null);
        sessionService.Setup(s => s.CreateSession()).Returns(("new-raw-token", "new-hash"));

        var middleware = new ShopperSessionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, sessionService.Object, cookieService.Object, sessionContext, options);

        sessionService.Verify(s => s.CreateSession(), Times.Once);
        cookieService.Verify(c => c.SetSessionCookie("new-raw-token", It.IsAny<DateTime>()), Times.Once);
        sessionContext.SessionHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task InvokeAsync_ExpiredToken_CreatesNewSession()
    {
        var (context, sessionService, cookieService, sessionContext, options) = CreateSetup();
        cookieService.Setup(c => c.ReadSessionToken()).Returns("expired-token");
        sessionService.Setup(s => s.ValidateAndGetHash(It.IsAny<string>())).Returns((string?)null);
        sessionService.Setup(s => s.CreateSession()).Returns(("fresh-raw", "fresh-hash"));

        var middleware = new ShopperSessionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, sessionService.Object, cookieService.Object, sessionContext, options);

        sessionService.Verify(s => s.CreateSession(), Times.Once);
        sessionContext.SessionHash.Should().Be("fresh-hash");
    }

    [Fact]
    public async Task InvokeAsync_WhitespaceToken_CreatesNewSession()
    {
        var (context, sessionService, cookieService, sessionContext, options) = CreateSetup();
        cookieService.Setup(c => c.ReadSessionToken()).Returns("   ");
        sessionService.Setup(s => s.ValidateAndGetHash(It.IsAny<string>())).Returns((string?)null);
        sessionService.Setup(s => s.CreateSession()).Returns(("raw", "hash"));

        var middleware = new ShopperSessionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, sessionService.Object, cookieService.Object, sessionContext, options);

        sessionService.Verify(s => s.CreateSession(), Times.Once);
        sessionContext.SessionHash.Should().Be("hash");
    }
}
