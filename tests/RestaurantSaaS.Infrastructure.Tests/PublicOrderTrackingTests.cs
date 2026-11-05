using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class PublicOrderTrackingTests
{
    private static ApplicationDbContext CreateInMemoryDbWithOrder(
        out Guid tenantId,
        out Guid restaurantId,
        out Guid menuItemId,
        out string rawTrackingToken,
        out string trackingTokenHash,
        OrderStatus status = OrderStatus.New)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var now = DateTime.UtcNow;

        var tenant = Tenant.Create("Test Tenant", "test-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        tenantId = tenant.Id;

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(now);

        var db = new ApplicationDbContext(options, tenantContextMock.Object, dateTimeProvider.Object);
        db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenant.Id, "Test Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        restaurantId = restaurant.Id;
        db.Restaurants.Add(restaurant);

        var subscription = Subscription.Start(
            tenantId,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            now.AddDays(-1),
            now.AddDays(30));
        db.Subscriptions.Add(subscription);

        var menu = Menu.Create(tenantId, restaurantId, "Main Menu");
        menu.SetActive(true);
        menu.Publish(1);
        db.Menus.Add(menu);

        var category = MenuCategory.Create(menu.Id, "Main");
        db.MenuCategories.Add(category);

        var menuItem = MenuItem.Create(
            category.Id,
            "Test Burger",
            Money.New(450m));
        menuItemId = menuItem.Id;
        db.MenuItems.Add(menuItem);

        db.SaveChanges();

        // Generate a real tracking token + hash
        var tokenService = new OrderTrackingTokenService();
        (rawTrackingToken, trackingTokenHash) = tokenService.Generate();

        // Create an order directly (bypassing PublicOrderService to focus on tracking)
        var orderItem = OrderItem.CreateSnapshot(
            Guid.NewGuid(),
            menuItemId,
            "Test Burger",
            450m,
            2,
            "PKR");

        var order = Order.Create(
            tenantId,
            restaurantId,
            null,
            "ORD-20261105-ABC123",
            "John Doe",
            "+923001234567",
            "john@example.com",
            900m,
            0m,
            0m,
            900m,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { orderItem });

        order.SetTrackingTokenHash(trackingTokenHash);
        db.Orders.Add(order);
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task GetOrderByTrackingTokenAsync_ValidToken_ReturnsOrder()
    {
        using var db = CreateInMemoryDbWithOrder(
            out var tenantId, out var restaurantId, out _, out var rawToken, out var _);

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var service = new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());

        var result = await service.GetOrderByTrackingTokenAsync(restaurantId, rawToken);

        result.Should().NotBeNull();
        result!.OrderNumber.Should().Be("ORD-20261105-ABC123");
        result.Status.Should().Be(OrderStatus.New);
        result.TotalAmount.Should().Be(900m);
        result.Items.Should().HaveCount(1);
        result.Items[0].ItemName.Should().Be("Test Burger");
        result.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetOrderByTrackingTokenAsync_InvalidToken_ReturnsNull()
    {
        using var db = CreateInMemoryDbWithOrder(
            out var tenantId, out var restaurantId, out _, out _, out var _);

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var service = new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());

        var result = await service.GetOrderByTrackingTokenAsync(restaurantId, "invalid-token-that-does-not-match");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderByTrackingTokenAsync_WrongRestaurant_ReturnsNull()
    {
        using var db = CreateInMemoryDbWithOrder(
            out var tenantId, out var restaurantId, out _, out var rawToken, out var _);

        var otherRestaurantId = Guid.NewGuid();

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var service = new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());

        var result = await service.GetOrderByTrackingTokenAsync(otherRestaurantId, rawToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderByTrackingTokenAsync_ResponseDoesNotExposeRawToken()
    {
        using var db = CreateInMemoryDbWithOrder(
            out var tenantId, out var restaurantId, out _, out var rawToken, out var _);

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var service = new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());

        var result = await service.GetOrderByTrackingTokenAsync(restaurantId, rawToken);

        result.Should().NotBeNull();
        // PublicOrderTrackingDto has no raw tracking token property - verified at compile time
        result!.OrderNumber.Should().NotBe(rawToken);
        result.Items.Should().NotContain(i => i.ItemName == rawToken);
    }

    [Fact]
    public async Task GetOrderByTrackingTokenAsync_CrossRestaurant_AccessDenied()
    {
        // Create an order in restaurant A
        using var db = CreateInMemoryDbWithOrder(
            out var tenantId, out var restaurantAId, out _, out var rawToken, out var _);

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var service = new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());

        // Same valid token, but requesting through a different restaurant slug (restaurant B)
        var otherRestaurantId = Guid.NewGuid();
        var result = await service.GetOrderByTrackingTokenAsync(otherRestaurantId, rawToken);

        result.Should().BeNull();
    }
}
