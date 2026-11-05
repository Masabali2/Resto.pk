using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

public class CartTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Create_WhenRestaurantIdAssigned_SetsRestaurantId()
    {
        var cart = Cart.Create(TenantId, RestaurantId, CustomerId, null, DateTime.UtcNow);

        cart.RestaurantId.Should().Be(RestaurantId);
    }

    [Fact]
    public void Create_WhenEmptyRestaurantId_Throws()
    {
        var act = () => Cart.Create(TenantId, Guid.Empty, CustomerId, null, DateTime.UtcNow);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CART_RESTAURANT_REQUIRED");
    }

    [Fact]
    public void Create_WhenGuestSession_AllowsNoCustomer()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);

        cart.CustomerId.Should().BeNull();
        cart.SessionId.Should().Be("session-1");
    }

    [Fact]
    public void Create_WhenAuthenticatedCustomer_AllowsNoSession()
    {
        var cart = Cart.Create(TenantId, RestaurantId, CustomerId, null, DateTime.UtcNow);

        cart.CustomerId.Should().Be(CustomerId);
        cart.SessionId.Should().BeNull();
    }

    [Fact]
    public void Create_WhenNoOwner_Throws()
    {
        var act = () => Cart.Create(TenantId, RestaurantId, null, null, DateTime.UtcNow);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CART_OWNER_REQUIRED");
    }
}
