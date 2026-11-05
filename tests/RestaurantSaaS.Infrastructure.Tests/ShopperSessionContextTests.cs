using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class ShopperSessionContextTests
{
    private static (ShopperSessionContext context, DefaultHttpContext httpContext) CreateContext()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return (new ShopperSessionContext(accessor.Object), httpContext);
    }

    [Fact]
    public void SessionHash_GetSet_ReturnsValue()
    {
        var (context, _) = CreateContext();

        context.SessionHash = "test-hash-123";

        context.SessionHash.Should().Be("test-hash-123");
    }

    [Fact]
    public void HasSession_True_WhenSessionHashSet()
    {
        var (context, _) = CreateContext();
        context.SessionHash = "test-hash";

        context.HasSession.Should().BeTrue();
    }

    [Fact]
    public void HasSession_False_WhenSessionHashNull()
    {
        var (context, _) = CreateContext();

        context.HasSession.Should().BeFalse();
    }

    [Fact]
    public void SessionHash_SetToNull_RemovesHash()
    {
        var (context, _) = CreateContext();
        context.SessionHash = "test-hash";
        context.SessionHash.Should().Be("test-hash");

        context.SessionHash = null;

        context.SessionHash.Should().BeNull();
        context.HasSession.Should().BeFalse();
    }

    [Fact]
    public void SessionHash_StoredInHttpContextItems()
    {
        var (context, httpContext) = CreateContext();
        context.SessionHash = "stored-hash";

        httpContext.Items["resto_shopper_session_hash"].Should().Be("stored-hash");
    }
}
