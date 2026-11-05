using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Enums;
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

    [Fact]
    public void EnsureUsable_WhenExpired_ThrowsCartExpired()
    {
        var pastTime = DateTime.UtcNow.AddDays(-2);
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", pastTime);
        // ExpiresAt = pastTime + 24h = yesterday, which is already expired

        var act = () => cart.EnsureUsable(DateTime.UtcNow);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CART_EXPIRED");
    }

    [Fact]
    public void EnsureUsable_WhenConverted_ThrowsCartNotActive()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);
        cart.MarkConverted(DateTime.UtcNow);

        var act = () => cart.EnsureUsable(DateTime.UtcNow);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CART_NOT_ACTIVE");
    }

    [Fact]
    public void MarkConverted_WhenActive_SetsStatusAndUpdatedAt()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);

        cart.MarkConverted(DateTime.UtcNow.AddSeconds(1));

        cart.Status.Should().Be(CartStatus.Converted);
        cart.UpdatedAt.Should().NotBeNull();
        cart.UpdatedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkConverted_WhenAlreadyConverted_Throws()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);
        cart.MarkConverted(DateTime.UtcNow);

        var act = () => cart.MarkConverted(DateTime.UtcNow.AddSeconds(1));

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "CART_NOT_ACTIVE");
    }

    [Fact]
    public void Renew_WhenActive_ExtendsExpiresAt()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);
        var originalExpiry = cart.ExpiresAt;

        cart.Renew(DateTime.UtcNow.AddHours(1));

        cart.ExpiresAt.Should().BeAfter(originalExpiry);
    }

    [Fact]
    public void MarkExpired_WhenActive_SetsStatusToExpired()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);

        cart.MarkExpired(DateTime.UtcNow.AddDays(1));

        cart.Status.Should().Be(CartStatus.Expired);
    }

    [Fact]
    public void MarkExpired_WhenAlreadyConverted_DoesNotChangeStatus()
    {
        var cart = Cart.Create(TenantId, RestaurantId, null, "session-1", DateTime.UtcNow);
        cart.MarkConverted(DateTime.UtcNow);

        cart.MarkExpired(DateTime.UtcNow.AddDays(1));

        cart.Status.Should().Be(CartStatus.Converted);
    }
}
