using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class PublicOrderServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbWithCart(
        out Guid tenantId,
        out Guid restaurantId,
        out Guid menuItemId,
        out string sessionHash,
        int quantity = 2)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var now = DateTime.UtcNow;
        sessionHash = "test-session-hash-123";

        var tenant = Tenant.Create("Test Tenant", "test-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        tenantId = tenant.Id;

        var tenantContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
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

        // Create a cart with items using the public cart service (skip if quantity <= 0)
        if (quantity > 0)
        {
            var sessionContextMock = new Mock<IShopperSessionContext>();
            sessionContextMock.Setup(s => s.SessionHash).Returns(sessionHash);

            var cartService = new PublicCartService(
                db,
                sessionContextMock.Object,
                tenantContextMock.Object,
                dateTimeProvider.Object);

            cartService.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, quantity))
                .GetAwaiter()
                .GetResult();
        }

        return db;
    }

    private static PublicOrderService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Mock<IShopperSessionContext>? sessionContextMock = null,
        Mock<ITenantContext>? tenantContextMock = null,
        Mock<IDateTimeProvider>? dateTimeProviderMock = null,
        Mock<IOrderNumberGenerator>? orderNumberGeneratorMock = null,
        Mock<IOrderTrackingTokenService>? trackingTokenServiceMock = null)
    {
        sessionContextMock ??= new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash-123");

        tenantContextMock ??= new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        dateTimeProviderMock ??= new Mock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        orderNumberGeneratorMock ??= new Mock<IOrderNumberGenerator>();
        orderNumberGeneratorMock.Setup(g => g.Generate()).Returns("ORD-TEST-123");

        trackingTokenServiceMock ??= new Mock<IOrderTrackingTokenService>();
        trackingTokenServiceMock.Setup(g => g.Generate()).Returns(("raw-token-abc123", "a7f5e3b2c9d1e4f6a8b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2"));

        return new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProviderMock.Object,
            orderNumberGeneratorMock.Object,
            trackingTokenServiceMock.Object);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_EmptyCart_Rejects()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out _, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        // Clear the cart to make it empty (cart still exists, just no items)
        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash-123");
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(It.IsAny<Guid>());
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(It.IsAny<Guid>());
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        var cartService = new PublicCartService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object);
        await cartService.ClearAsync(restaurantId);

        var act = () => service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", "+923001234567", null, null));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_ValidCart_CreatesOrderWithServerTotals()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 2);
        var service = CreateService(db, tenantId);

        var response = await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", "+923001234567", "john@example.com", null));

        response.Should().NotBeNull();
        response.OrderNumber.Should().Be("ORD-TEST-123");
        response.Status.Should().Be(OrderStatus.New);
        // Subtotal 900 + tax 144 (16%) = 1044
        response.TotalAmount.Should().Be(1044m);
        response.Currency.Should().Be("PKR");
        response.Items.Should().HaveCount(1);
        response.Items[0].UnitPrice.Should().Be(450m);
        response.Items[0].Quantity.Should().Be(2);
        response.Items[0].LineTotal.Should().Be(900m);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_ValidCart_UsesRequestOrderType()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        var response = await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.DineIn, "John Doe", null, null, null));

        response.Should().NotBeNull();
        // Verify order type is stored by checking the persisted order
        var order = await db.Orders.FirstAsync();
        order.OrderType.Should().Be(OrderType.DineIn);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_ConvertedCart_RejectsDuplicate()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        // First checkout succeeds
        await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        // Second checkout with same cart should be rejected
        var act = () => service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_ValidDirectOrder_CreatesOrderWithServerTotals()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            "+923001234567",
            "john@example.com",
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 2) });

        var response = await service.CreateDirectOrderAsync(restaurantId, request);

        response.Should().NotBeNull();
        response.Status.Should().Be(OrderStatus.New);
        // Subtotal 900 + tax 144 (16%) = 1044
        response.TotalAmount.Should().Be(1044m);
        response.Currency.Should().Be("PKR");
        response.Items.Should().HaveCount(1);
        response.Items[0].UnitPrice.Should().Be(450m);
        response.Items[0].Quantity.Should().Be(2);
        response.Items[0].LineTotal.Should().Be(900m);
        response.RawTrackingToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_EmptyItems_Rejects()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out _);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest>());

        var act = () => service.CreateDirectOrderAsync(restaurantId, request);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_MenuItemNotFound_Rejects()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out _);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(Guid.NewGuid(), 1) });

        var act = () => service.CreateDirectOrderAsync(restaurantId, request);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_CrossRestaurantItem_Rejects()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        // Create a second restaurant in the same tenant
        var address2 = Address.Create("456 Other St", "Lahore", "Pakistan");
        var otherRestaurant = Restaurant.Create(tenantId, "Other Restaurant", address2, taxRate: 10m);
        otherRestaurant.ChangeStatus(RestaurantStatus.Active);
        var otherRestaurantId = otherRestaurant.Id;
        db.Restaurants.Add(otherRestaurant);
        db.SaveChanges();

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 1) });

        var act = () => service.CreateDirectOrderAsync(otherRestaurantId, request);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_UnavailableItem_Rejects()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        // Make the menu item unavailable
        var menuItem = await db.MenuItems.FirstAsync();
        menuItem.SetAvailability(false);
        await db.SaveChangesAsync();

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 1) });

        var act = () => service.CreateDirectOrderAsync(restaurantId, request);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateDirectOrderAsync_ServerAuthoritativePrices()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        // Menu item price is 450; client cannot override
        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 3) });

        var response = await service.CreateDirectOrderAsync(restaurantId, request);

        response.Items[0].UnitPrice.Should().Be(450m);
        response.Items[0].LineTotal.Should().Be(1350m);
        // Subtotal 1350 + tax 216 (16%) = 1566
        response.TotalAmount.Should().Be(1566m);
    }

    [Fact]
    public async Task CreateDirectOrderAsync_CorrectOrderType()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Delivery,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 1) });

        var response = await service.CreateDirectOrderAsync(restaurantId, request);

        var order = await db.Orders.FirstAsync();
        order.OrderType.Should().Be(OrderType.Delivery);
    }

    [Fact]
    public async Task CreateDirectOrderAsync_CashPendingInitialization()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 1) });

        await service.CreateDirectOrderAsync(restaurantId, request);

        var order = await db.Orders.FirstAsync();
        order.PaymentMethod.Should().Be(PaymentMethod.Cash);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task CreateDirectOrderAsync_TrackingHashPersistedRawTokenReturned()
    {
        using var db = CreateInMemoryDbForDirectOrder(out var tenantId, out var restaurantId, out var menuItemId);
        var service = CreateService(db, tenantId);

        var request = new CreateDirectPublicOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new List<DirectPublicOrderItemRequest> { new(menuItemId, 1) });

        var response = await service.CreateDirectOrderAsync(restaurantId, request);

        var order = await db.Orders.FirstAsync();
        order.TrackingTokenHash.Should().NotBeNullOrWhiteSpace();
        order.TrackingTokenHash.Should().HaveLength(64);
        order.TrackingTokenHash.Should().NotBe(response.RawTrackingToken);
        response.RawTrackingToken.Should().NotBeNullOrWhiteSpace();
    }

    private static ApplicationDbContext CreateInMemoryDbForDirectOrder(
        out Guid tenantId,
        out Guid restaurantId,
        out Guid menuItemId)
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

        return db;
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_RawTrackingTokenReturnedOnce()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        var response = await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        response.RawTrackingToken.Should().Be("raw-token-abc123");
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_TrackingTokenHashPersisted()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        var order = await db.Orders.FirstAsync();
        order.TrackingTokenHash.Should().Be("a7f5e3b2c9d1e4f6a8b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2");
        order.TrackingTokenHash.Should().NotBe("raw-token-abc123");
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_RawTokenNotStoredInOrderEntity()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        var order = await db.Orders.FirstAsync();
        order.TrackingTokenHash.Should().NotBe("raw-token-abc123");
        order.TrackingTokenHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_CartConvertedAfterOrder()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        await service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        var cart = await db.Carts.FirstAsync();
        cart.Status.Should().Be(CartStatus.Converted);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_UnavailableItemPreventsOrder()
    {
        using var db = CreateInMemoryDbWithCart(out var tenantId, out var restaurantId, out var menuItemId, out _, quantity: 1);
        var service = CreateService(db, tenantId);

        // Make the menu item unavailable
        var menuItem = await db.MenuItems.FirstAsync();
        menuItem.SetAvailability(false);
        await db.SaveChangesAsync();

        var act = () => service.CreateOrderFromCartAsync(
            restaurantId,
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null));

        await act.Should().ThrowAsync<DomainException>();
    }
}
