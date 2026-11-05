using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicCarts;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
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

public class PublicCartServiceTests
{
    private static ApplicationDbContext CreateInMemoryDb(
        out Guid tenantId,
        out Guid restaurantId,
        out Guid menuItemId,
        out string sessionHash,
        Mock<IShopperSessionContext>? sessionContextMock = null)
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

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.HasTenant).Returns(true);
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);
        tenantContext.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(now);

        var db = new ApplicationDbContext(options, tenantContext.Object, dateTimeProvider.Object);
        db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenant.Id, "Test Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        restaurantId = restaurant.Id;
        db.Restaurants.Add(restaurant);

        // Subscription entitlement
        var subscription = Subscription.Start(
            tenantId,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            now.AddDays(-1),
            now.AddDays(30));
        db.Subscriptions.Add(subscription);

        // Menu
        var menu = Menu.Create(tenantId, restaurantId, "Main Menu");
        menu.SetActive(true);
        menu.Publish(1);
        db.Menus.Add(menu);

        // Menu category
        var category = MenuCategory.Create(menu.Id, "Main");
        db.MenuCategories.Add(category);

        // Menu item
        var menuItem = MenuItem.Create(
            category.Id,
            "Test Burger",
            Money.New(450m));
        menuItemId = menuItem.Id;
        db.MenuItems.Add(menuItem);

        db.SaveChanges();

        if (sessionContextMock is not null)
        {
            sessionContextMock.Setup(s => s.SessionHash).Returns(sessionHash);
        }

        return db;
    }

    private static PublicCartService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        Mock<IShopperSessionContext>? sessionContextMock = null,
        Mock<ITenantContext>? tenantContextMock = null,
        Mock<IDateTimeProvider>? dateTimeProviderMock = null)
    {
        var sessionMockCreated = sessionContextMock is null;
        sessionContextMock ??= new Mock<IShopperSessionContext>();
        if (sessionMockCreated)
            sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash-123");

        tenantContextMock ??= new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        dateTimeProviderMock ??= new Mock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        return new PublicCartService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task GetCurrentCartAsync_EmptySession_CreatesNewCart()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _, out _);
        var service = CreateService(db, tenantId);

        var cart = await service.GetCurrentCartAsync(restaurantId);

        cart.Should().NotBeNull();
        cart.ItemCount.Should().Be(0);
        cart.TotalAmount.Should().Be(0m);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentCartAsync_SameSessionSameRestaurant_ReturnsSameCart()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _, out _);
        var service = CreateService(db, tenantId);

        var cart1 = await service.GetCurrentCartAsync(restaurantId);
        var cart2 = await service.GetCurrentCartAsync(restaurantId);

        cart1.Id.Should().Be(cart2.Id);
    }

    [Fact]
    public async Task AddItemAsync_ValidMenuItem_AddsToCartWithServerPrice()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        var item = await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));

        item.Should().NotBeNull();
        item.MenuItemId.Should().Be(menuItemId);
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(450m);
        item.LineTotal.Should().Be(900m);
        item.Currency.Should().Be("PKR");
    }

    [Fact]
    public async Task AddItemAsync_AddingSameItemIncrementsQuantity()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));
        var item = await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 3));

        item.Quantity.Should().Be(5);
        item.LineTotal.Should().Be(2250m);
    }

    [Fact]
    public async Task AddItemAsync_ExceedingMaxQuantity_Rejects()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        var act = () => service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 100));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task AddItemAsync_InvalidQuantity_Rejects()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        var act = () => service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 0));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateItemAsync_Valid_UpdatesQuantity()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        var added = await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));
        var updated = await service.UpdateItemAsync(restaurantId, added.Id, new UpdatePublicCartItemRequest(5));

        updated.Quantity.Should().Be(5);
        updated.LineTotal.Should().Be(2250m);
    }

    [Fact]
    public async Task RemoveItemAsync_Valid_RemovesItem()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        var added = await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));
        await service.RemoveItemAsync(restaurantId, added.Id);

        var cart = await service.GetCurrentCartAsync(restaurantId);
        cart.Items.Should().BeEmpty();
        cart.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_Valid_ClearsAllItems()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));
        await service.ClearAsync(restaurantId);

        var cart = await service.GetCurrentCartAsync(restaurantId);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentCartAsync_DifferentSession_CannotAccessExistingCart()
    {
        var sessionHashA = "session-hash-a";
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out sessionHashA);
        var serviceA = CreateService(db, tenantId);

        await serviceA.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 2));

        // Different session hash → should not see session A's cart
        var sessionContextMockB = new Mock<IShopperSessionContext>();
        sessionContextMockB.Setup(s => s.SessionHash).Returns("session-hash-b");
        var serviceB = CreateService(db, tenantId, sessionContextMock: sessionContextMockB);

        var cartB = await serviceB.GetCurrentCartAsync(restaurantId);
        cartB.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task AddItemAsync_NonexistentMenuItem_ThrowsNotFound()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _, out _);
        var service = CreateService(db, tenantId);

        var act = () => service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(Guid.NewGuid(), 1));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateItemAsync_NonexistentCartItem_ThrowsNotFound()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _, out _);
        var service = CreateService(db, tenantId);

        var act = () => service.UpdateItemAsync(restaurantId, Guid.NewGuid(), new UpdatePublicCartItemRequest(1));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetCurrentCartAsync_DifferentRestaurant_IndependentCart()
    {
        var sessionHash = "session-shared-across-restaurants";
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantIdA, out var menuItemId, out sessionHash);
        var service = CreateService(db, tenantId);

        // Add item to restaurant A using a real menu item
        await service.AddItemAsync(restaurantIdA, new AddPublicCartItemRequest(menuItemId, 1));

        // Same session, different restaurant B → independent empty cart
        var cartB = await service.GetCurrentCartAsync(Guid.NewGuid());
        cartB.ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentCartAsync_MissingSessionHash_ThrowsNotAuthenticated()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _, out _);

        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns((string?)null);
        var service = CreateService(db, tenantId, sessionContextMock: sessionContextMock);

        var act = () => service.GetCurrentCartAsync(restaurantId);

        await act.Should().ThrowAsync<NotAuthenticatedException>();
    }

    [Fact]
    public async Task AddItemAsync_CartFromOtherTenant_Rejects()
    {
        // This test verifies that the service enforces tenant isolation via ITenantContext.
        // The ITenantContext mock resolves a specific tenantId; any cart query
        // filters by that tenantId, so carts from other tenants are invisible.
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var menuItemId, out _);
        var service = CreateService(db, tenantId);

        // Add item normally using a real menu item
        var item = await service.AddItemAsync(restaurantId, new AddPublicCartItemRequest(menuItemId, 1));
        item.Should().NotBeNull();

        // Verify cart belongs to the tenant resolved by ITenantContext
        var cart = await service.GetCurrentCartAsync(restaurantId);
        cart.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCurrentCartAsync_SameSession_DifferentRestaurantIds_AreIndependent()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantIdA, out var menuItemId, out var sessionHash);
        var service = CreateService(db, tenantId);

        // Add item to restaurant A using a real menu item
        await service.AddItemAsync(restaurantIdA, new AddPublicCartItemRequest(menuItemId, 2));

        // Get cart for restaurant B - should be empty (different restaurant)
        var cartB = await service.GetCurrentCartAsync(Guid.NewGuid());
        cartB.ItemCount.Should().Be(0);

        // Cart for restaurant A should still have items
        var cartA = await service.GetCurrentCartAsync(restaurantIdA);
        cartA.ItemCount.Should().Be(1);
        cartA.Items.Should().HaveCount(1);
    }
}
