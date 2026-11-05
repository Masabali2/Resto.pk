using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Api.Tests;

public class ShopperSessionMiddlewareTests
{
    private static ShopperSessionOptions CreateOptions(
        int lifetimeHours = 24,
        string secret = "this-is-a-test-secret-key-that-is-at-least-32-chars-long!")
    {
        return new ShopperSessionOptions
        {
            LifetimeHours = lifetimeHours,
            Secret = secret,
            SameSite = "Lax",
            Secure = false,
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    [Fact]
    public async Task InvokeAsync_WithNoCookie_CreatesNewSessionAndSetsCookie()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var sessionService = new ShopperSessionService(options, new Mock<IDateTimeProvider>().Object);
        var cookieService = new Mock<IShopperSessionCookieService>();
        var sessionContext = new Mock<IShopperSessionContext>();

        var middleware = new ShopperSessionMiddleware(async (ctx) => await Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, sessionService, cookieService.Object, sessionContext.Object, options);

        // Assert
        cookieService.Verify(c => c.SetSessionCookie(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        sessionContext.VerifySet(s => s.SessionHash = It.Is<string>(h => !string.IsNullOrWhiteSpace(h)), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithValidCookie_PreservesExistingSession()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var sessionService = new ShopperSessionService(options, new Mock<IDateTimeProvider>().Object);
        var cookieService = new Mock<IShopperSessionCookieService>();
        var sessionContext = new Mock<IShopperSessionContext>();

        // Create a valid session token
        var (rawToken, sessionHash) = sessionService.CreateSession();
        cookieService.Setup(c => c.ReadSessionToken()).Returns(rawToken);

        var middleware = new ShopperSessionMiddleware(async (ctx) => await Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, sessionService, cookieService.Object, sessionContext.Object, options);

        // Assert - existing session hash should be used, not a new one
        sessionContext.VerifySet(s => s.SessionHash = sessionHash, Times.Once);
        cookieService.Verify(c => c.SetSessionCookie(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidCookie_CreatesNewSession()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var sessionService = new ShopperSessionService(options, new Mock<IDateTimeProvider>().Object);
        var cookieService = new Mock<IShopperSessionCookieService>();
        var sessionContext = new Mock<IShopperSessionContext>();

        cookieService.Setup(c => c.ReadSessionToken()).Returns("invalid-token");

        var middleware = new ShopperSessionMiddleware(async (ctx) => await Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, sessionService, cookieService.Object, sessionContext.Object, options);

        // Assert - invalid cookie should be replaced with new session
        cookieService.Verify(c => c.SetSessionCookie(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        sessionContext.VerifySet(s => s.SessionHash = It.Is<string>(h => !string.IsNullOrWhiteSpace(h)), Times.Once);
    }

    [Fact]
    public void SessionHash_IsNeverNullOrWhiteSpace()
    {
        var options = Options.Create(CreateOptions());
        var sessionService = new ShopperSessionService(options, new Mock<IDateTimeProvider>().Object);

        var (rawToken, hash) = sessionService.CreateSession();

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().HaveLength(64);
        rawToken.Should().NotBe(hash);
    }
}

public class OriginValidationMiddlewareTests
{
    private static ShopperSessionOptions CreateOptions(
        ISet<string>? allowedOrigins = null)
    {
        return new ShopperSessionOptions
        {
            LifetimeHours = 24,
            Secret = "this-is-a-test-secret-key-that-is-at-least-32-chars-long!",
            SameSite = "Lax",
            Secure = false,
            AllowedOrigins = allowedOrigins ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };
    }

    [Fact]
    public async Task InvokeAsync_WithSafeMethod_AllowsRequestRegardlessOfOrigin()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var middleware = new OriginValidationMiddleware(
            async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Headers.Cookie = "resto_shopper_session=some-token";

        // Act
        await middleware.InvokeAsync(httpContext, options);

        // Assert
        httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeMethod_NoCookie_AllowsRequest()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var middleware = new OriginValidationMiddleware(
            async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        // No shopper session cookie

        // Act
        await middleware.InvokeAsync(httpContext, options);

        // Assert
        httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeMethod_CookieButNoOrigin_Returns403()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var middleware = new OriginValidationMiddleware(
            async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers.Cookie = "resto_shopper_session=some-token";
        // No Origin header

        // Act
        await middleware.InvokeAsync(httpContext, options);

        // Assert
        httpContext.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeMethod_CookieAndAllowedOrigin_AllowsRequest()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var middleware = new OriginValidationMiddleware(
            async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers.Cookie = "resto_shopper_session=some-token";
        httpContext.Request.Headers.Origin = "https://localhost:4200";

        // Act
        await middleware.InvokeAsync(httpContext, options);

        // Assert
        httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeMethod_CookieAndDisallowedOrigin_Returns403()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var middleware = new OriginValidationMiddleware(
            async (ctx) => { ctx.Response.StatusCode = 200; await Task.CompletedTask; });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers.Cookie = "resto_shopper_session=some-token";
        httpContext.Request.Headers.Origin = "https://evil.com";

        // Act
        await middleware.InvokeAsync(httpContext, options);

        // Assert
        httpContext.Response.StatusCode.Should().Be(403);
    }
}
