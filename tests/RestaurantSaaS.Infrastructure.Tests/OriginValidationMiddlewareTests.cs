using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Infrastructure.Tests;

public class OriginValidationMiddlewareTests
{
    private static ShopperSessionOptions CreateOptions(ISet<string>? allowedOrigins = null) =>
        new()
        {
            CookieName = "resto_shopper_session",
            LifetimeHours = 24,
            Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SameSite = "Lax",
            Secure = true,
            AllowedOrigins = allowedOrigins ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };

    private static DefaultHttpContext CreateContext(string method, string? origin = null, bool withCookie = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        if (withCookie)
            context.Request.Headers.Cookie = "resto_shopper_session=some-token";
        return context;
    }

    private static async Task<int> InvokeMiddleware(
        DefaultHttpContext context,
        ShopperSessionOptions options)
    {
        var opts = Options.Create(options);
        var middleware = new OriginValidationMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, opts);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task Get_WithoutCookie_PassesThrough()
    {
        var context = CreateContext("GET");
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Get_WithCookie_PassesThrough()
    {
        var context = CreateContext("GET", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Post_WithCookie_AllowedOrigin_PassesThrough()
    {
        var context = CreateContext("POST", "https://localhost:4200", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Post_WithCookie_DisallowedOrigin_Returns403()
    {
        var context = CreateContext("POST", "https://evil.com", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_WithCookie_MissingOrigin_Returns403()
    {
        var context = CreateContext("POST", origin: null, withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_WithCookie_EmptyOrigin_Returns403()
    {
        var context = CreateContext("POST", "", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_WithCookie_EmptyAllowedOrigins_Returns403_FailSafely()
    {
        var context = CreateContext("POST", "https://localhost:4200", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions(allowedOrigins: new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_WithoutCookie_PassesThrough_RegardlessOfOrigin()
    {
        var context = CreateContext("POST", "https://evil.com", withCookie: false);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Head_WithCookie_PassesThrough()
    {
        var context = CreateContext("HEAD", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Options_WithCookie_PassesThrough()
    {
        var context = CreateContext("OPTIONS", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Trace_WithCookie_PassesThrough()
    {
        var context = CreateContext("TRACE", withCookie: true);
        var status = await InvokeMiddleware(context, CreateOptions());

        status.Should().Be(200);
    }

    [Fact]
    public async Task Post_WithCookie_OriginCaseInsensitiveMatch()
    {
        var options = CreateOptions();
        var context = CreateContext("POST", "HTTPS://LOCALHOST:4200", withCookie: true);
        var status = await InvokeMiddleware(context, options);

        status.Should().Be(200);
    }
}
