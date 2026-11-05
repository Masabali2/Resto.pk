using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// EF Core save interceptor that coordinates concurrent SaveChangesAsync calls
/// between two DbContext instances for SQL Server rowversion concurrency testing.
/// </summary>
public sealed class ConcurrencyBarrierInterceptor : SaveChangesInterceptor
{
    private static int _waitingCount;
    private static readonly object _lock = new();
    private static ManualResetEventSlim _releaseEvent = new(false);

    public static void Activate(int expectedWaiters)
    {
        lock (_lock)
        {
            _waitingCount = expectedWaiters;
            _releaseEvent = new ManualResetEventSlim(false);
        }
    }

    public static void Deactivate()
    {
        lock (_lock)
        {
            _waitingCount = 0;
            _releaseEvent.Set();
        }
    }

    public static void SignalReadyAndWait(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _waitingCount--;
            if (_waitingCount <= 0)
                _releaseEvent.Set();
        }

        if (!_releaseEvent.Wait(5000, ct))
            throw new TimeoutException("Concurrency barrier timed out waiting for release.");
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SignalReadyAndWait();
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        SignalReadyAndWait(ct);
        return new ValueTask<InterceptionResult<int>>(base.SavingChangesAsync(eventData, result, ct).AsTask());
    }
}

public class SqlServerConcurrencyTests
{
    private const string ConnectionStringEnvVar = "RESTO_SQLSERVER_TEST_CONNECTION";

    [Fact]
    public async Task ConcurrentCheckout_OnlyOneOrderPersisted()
    {
        // Opt-in test: requires a dedicated SQL Server test database
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Test is skipped - not a failure. SQL Server concurrency verification
            // is deferred until a dedicated test database is configured.
            return;
        }

        // NOTE: This test does NOT call Database.Migrate() or EnsureCreated()
        // against the supplied connection. The test database must be prepared
        // separately using the existing migrations before running this test:
        //
        //   dotnet ef database update --connection "$env:RESTO_SQLSERVER_TEST_CONNECTION"
        //
        // The connection string must point to an isolated test database that
        // is not used for development or production.

        // Create two independent DbContext instances using SQL Server
        var optionsA = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new ConcurrencyBarrierInterceptor())
            .Options;

        var optionsB = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new ConcurrencyBarrierInterceptor())
            .Options;

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(It.IsAny<Guid>());

        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        using var contextA = new ApplicationDbContext(optionsA, tenantContextMock.Object, dateTimeProviderMock.Object);
        using var contextB = new ApplicationDbContext(optionsB, tenantContextMock.Object, dateTimeProviderMock.Object);

        // Seed test data in contextA (shared database)
        await SeedTestDataAsync(contextA);

        // Get the cart ID from contextA
        var cart = await contextA.Carts
            .Include(c => c.Items)
            .FirstAsync(c => c.Status == CartStatus.Active);

        var cartId = cart.Id;
        var restaurantId = cart.RestaurantId!.Value;
        var tenantId = cart.TenantId;

        // Detach cart from contextA so contextB can load it independently
        contextA.Entry(cart).State = EntityState.Detached;

        // Create two independent service instances
        var serviceA = CreateService(contextA, tenantId);
        var serviceB = CreateService(contextB, tenantId);

        // Activate concurrency barrier for coordinated save timing (2 waiters)
        ConcurrencyBarrierInterceptor.Activate(2);

        var request = new CreatePublicOrderRequest(OrderType.Pickup, "Test Customer", null, null, null);

        // Start both checkouts concurrently
        var taskA = serviceA.CreateOrderFromCartAsync(restaurantId, request);
        var taskB = serviceB.CreateOrderFromCartAsync(restaurantId, request);

        var results = await Task.WhenAll(taskA, taskB);

        // Deactivate barrier
        ConcurrencyBarrierInterceptor.Deactivate();

        // Assert: exactly one order should have been created
        var orderCount = await contextA.Orders.CountAsync();
        orderCount.Should().Be(1, "Exactly one order should be persisted for one cart");

        // The cart should be converted
        var convertedCart = await contextA.Carts.FindAsync(cartId);
        convertedCart!.Status.Should().Be(CartStatus.Converted);

        // Cart items should be deleted
        var remainingItems = await contextA.CartItems.CountAsync(ci => ci.CartId == cartId);
        remainingItems.Should().Be(0, "Cart items should be deleted after conversion");
    }

    private static async Task SeedTestDataAsync(ApplicationDbContext db)
    {
        // Check if data already exists (idempotent seeding)
        if (await db.Tenants.AnyAsync())
            return;

        var tenant = Tenant.Create("Concurrency Test Tenant", "concurrency-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenant.Id, "Concurrency Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        db.Restaurants.Add(restaurant);

        var subscription = Subscription.Start(
            tenant.Id,
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30));
        db.Subscriptions.Add(subscription);

        await db.SaveChangesAsync();

        // Create menu and menu item for the restaurant
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Main Menu");
        menu.SetActive(true);
        menu.Publish(1);
        db.Menus.Add(menu);

        var category = MenuCategory.Create(menu.Id, "Main");
        db.MenuCategories.Add(category);

        var menuItem = MenuItem.Create(category.Id, "Test Burger", Money.New(450m));
        db.MenuItems.Add(menuItem);

        await db.SaveChangesAsync();

        // Create active cart
        var sessionHash = "test-session-hash-for-concurrency";
        var cart = Cart.Create(
            tenant.Id,
            restaurant.Id,
            null,
            sessionHash,
            DateTime.UtcNow);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        // Add cart item
        var cartItem = CartItem.Create(
            cart.Id,
            menuItem.Id,
            2,
            450m,
            "PKR");
        db.CartItems.Add(cartItem);

        await db.SaveChangesAsync();
    }

    private static PublicOrderService CreateService(ApplicationDbContext db, Guid tenantId)
    {
        var sessionContextMock = new Mock<IShopperSessionContext>();
        sessionContextMock.Setup(s => s.SessionHash).Returns("test-session-hash-for-concurrency");

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        return new PublicOrderService(
            db,
            sessionContextMock.Object,
            tenantContextMock.Object,
            dateTimeProvider.Object,
            new Mock<IOrderNumberGenerator>().Object,
            new OrderTrackingTokenService());
    }
}
