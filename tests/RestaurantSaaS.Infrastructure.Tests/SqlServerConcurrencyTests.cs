using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using Xunit;

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
        // Opt-in test: requires a dedicated SQL Server test database.
        // Skips explicitly (not silently) when the connection string is missing.
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail($"SQL Server concurrency test not executed: {ConnectionStringEnvVar} environment variable is not set. Configure a dedicated isolated test database and set this variable to run SQL Server concurrency verification.");
        }

        // NOTE: This test does NOT call Database.Migrate() or EnsureCreated()
        // against the supplied connection. The test database must be prepared
        // separately using the existing migrations before running this test.
        // The connection string must point to an isolated test database that
        // is not used for development or production.

        var tenant = Tenant.Create("Concurrency Test Tenant", $"concurrency-{Guid.NewGuid():N}");
        tenant.ChangeStatus(TenantStatus.Active);
        var tenantId = tenant.Id;
        var sessionHash = $"session-{Guid.NewGuid():N}";

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
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        using var contextA = new ApplicationDbContext(optionsA, tenantContextMock.Object, dateTimeProviderMock.Object);
        using var contextB = new ApplicationDbContext(optionsB, tenantContextMock.Object, dateTimeProviderMock.Object);

        try
        {
            // Seed isolated test data in contextA (shared database)
            await SeedTestDataAsync(contextA, tenant, sessionHash);

            // Get the cart ID from contextA
            var cart = await contextA.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.SessionId == sessionHash && c.Status == CartStatus.Active);

            var cartId = cart.Id;
            var restaurantId = cart.RestaurantId!.Value;

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

            // Await each task individually so the losing checkout's DomainException
            // does not fault Task.WhenAll and abort the test before assertions.
            Exception? exceptionA = null;
            Exception? exceptionB = null;
            CreatePublicOrderResponse? resultA = null;
            CreatePublicOrderResponse? resultB = null;

            try
            {
                resultA = await taskA;
            }
            catch (Exception ex)
            {
                exceptionA = ex;
            }

            try
            {
                resultB = await taskB;
            }
            catch (Exception ex)
            {
                exceptionB = ex;
            }
            finally
            {
                // Always release the barrier, even if assertions or DB operations fail.
                ConcurrencyBarrierInterceptor.Deactivate();
            }

            // Assert: exactly one checkout succeeded, one was rejected
            var successes = new List<CreatePublicOrderResponse>();
            var failures = new List<Exception>();

            if (resultA is not null) successes.Add(resultA);
            if (exceptionA is not null) failures.Add(exceptionA);
            if (resultB is not null) successes.Add(resultB);
            if (exceptionB is not null) failures.Add(exceptionB);

            successes.Should().HaveCount(1, "Exactly one checkout should succeed");
            failures.Should().HaveCount(1, "Exactly one checkout should be rejected with DomainException");
            failures[0].Should().BeOfType<DomainException>()
                .Which.Message.Should().Contain("CART_ALREADY_CONVERTED");

            // Assert: exactly one order was persisted
            var orderCount = await contextA.Orders.CountAsync(o => o.TenantId == tenantId);
            orderCount.Should().Be(1, "Exactly one order should be persisted for one cart");

            // The cart should be converted
            var convertedCart = await contextA.Carts.FindAsync(cartId);
            convertedCart.Should().NotBeNull();
            convertedCart!.Status.Should().Be(CartStatus.Converted, "Cart should be converted after successful checkout");

            // Cart items should be deleted
            var remainingItems = await contextA.CartItems.CountAsync(ci => ci.CartId == cartId);
            remainingItems.Should().Be(0, "Cart items should be deleted after conversion");

            // Clean up: remove the test order and cart
            var orderToRemove = await contextA.Orders.FirstOrDefaultAsync(o => o.TenantId == tenantId);
            if (orderToRemove is not null)
            {
                contextA.Orders.Remove(orderToRemove);
            }
            contextA.Carts.Remove(convertedCart);
            await contextA.SaveChangesAsync();
        }
        catch
        {
            // Ensure barrier is released even on unexpected failure
            ConcurrencyBarrierInterceptor.Deactivate();
            throw;
        }
    }

    [Fact]
    public async Task ConcurrentStaffStatusTransition_OnlyOneSucceeds()
    {
        // Opt-in test: requires a dedicated SQL Server test database.
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail($"SQL Server staff concurrency test not executed: {ConnectionStringEnvVar} environment variable is not set.");
        }

        var tenant = Tenant.Create("Staff Concurrency Tenant", $"staff-concurrency-{Guid.NewGuid():N}");
        tenant.ChangeStatus(TenantStatus.Active);
        var tenantId = tenant.Id;
        var userId = Guid.NewGuid();

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
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProviderMock = new Mock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        using var contextA = new ApplicationDbContext(optionsA, tenantContextMock.Object, dateTimeProviderMock.Object);
        using var contextB = new ApplicationDbContext(optionsB, tenantContextMock.Object, dateTimeProviderMock.Object);

        try
        {
            // Seed isolated test data: tenant, restaurant, order in New status
            var restaurantId = await SeedStaffConcurrencyTestData(contextA, tenant);

            // Get the order ID from contextA
            var order = await contextA.Orders
                .FirstAsync(o => o.TenantId == tenantId && o.Status == OrderStatus.New);

            var orderId = order.Id;

            // Detach order from contextA so contextB can load it independently
            contextA.Entry(order).State = EntityState.Detached;

            // Create two independent service instances
            var serviceA = CreateStaffService(contextA, tenantId, userId);
            var serviceB = CreateStaffService(contextB, tenantId, userId);

            // Activate concurrency barrier for coordinated save timing (2 waiters)
            ConcurrencyBarrierInterceptor.Activate(2);

            var request = new UpdateOrderStatusRequest(OrderStatus.Confirmed, null);

            // Start both transitions concurrently
            var taskA = serviceA.TransitionStatusAsync(restaurantId, orderId, request, userId);
            var taskB = serviceB.TransitionStatusAsync(restaurantId, orderId, request, userId);

            Exception? exceptionA = null;
            Exception? exceptionB = null;
            StaffOrderDto? resultA = null;
            StaffOrderDto? resultB = null;

            try
            {
                resultA = await taskA;
            }
            catch (Exception ex)
            {
                exceptionA = ex;
            }

            try
            {
                resultB = await taskB;
            }
            catch (Exception ex)
            {
                exceptionB = ex;
            }
            finally
            {
                ConcurrencyBarrierInterceptor.Deactivate();
            }

            // Assert: exactly one transition succeeded, one was rejected
            var successes = new List<StaffOrderDto>();
            var failures = new List<Exception>();

            if (resultA is not null) successes.Add(resultA);
            if (exceptionA is not null) failures.Add(exceptionA);
            if (resultB is not null) successes.Add(resultB);
            if (exceptionB is not null) failures.Add(exceptionB);

            successes.Should().HaveCount(1, "Exactly one status transition should succeed");
            failures.Should().HaveCount(1, "Exactly one transition should be rejected due to concurrency");

            // The losing transition should get a concurrency-related exception
            var losingEx = failures[0];
            losingEx.Should().BeOfType<DbUpdateConcurrencyException>();

            // Assert: order status in DB should be Confirmed (one successful transition)
            var finalOrder = await contextA.Orders.FindAsync(orderId);
            finalOrder.Should().NotBeNull();
            finalOrder!.Status.Should().Be(OrderStatus.Confirmed, "Order status should reflect the one successful transition");

            // Clean up
            contextA.Orders.Remove(finalOrder);
            await contextA.SaveChangesAsync();
        }
        catch
        {
            ConcurrencyBarrierInterceptor.Deactivate();
            throw;
        }
    }

    private static async Task<Guid> SeedStaffConcurrencyTestData(ApplicationDbContext db, Tenant tenant)
    {
        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenant.Id, "Staff Concurrency Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);

        db.Tenants.Add(tenant);
        db.Restaurants.Add(restaurant);

        var plan = SubscriptionPlan.Create(
            $"staff-concurrency-{tenant.Id:N}",
            $"Staff Concurrency Plan {tenant.Id:N}",
            Money.Zero(),
            BillingInterval.Monthly,
            maxStaff: 5,
            maxMenuItems: 100,
            maxOrdersPerMonth: 500);
        db.SubscriptionPlans.Add(plan);

        var subscription = Subscription.Start(
            tenant.Id,
            plan.Id,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30));
        db.Subscriptions.Add(subscription);

        await db.SaveChangesAsync();

        var orderItem = OrderItem.CreateSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Test Burger", 450m, 1, "PKR");

        var order = Order.Create(
            tenant.Id,
            restaurant.Id,
            null,
            $"ORD-STAFF-{Guid.NewGuid():N}",
            "Test Customer",
            null,
            null,
            450m,
            0m,
            0m,
            450m,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { orderItem });

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return restaurant.Id;
    }

    private static StaffOrderService CreateStaffService(ApplicationDbContext db, Guid tenantId, Guid userId)
    {
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
        currentUserMock.Setup(c => c.UserId).Returns(userId);

        var membershipMock = new Mock<ITenantMembershipService>();
        membershipMock.Setup(m => m.GetMembershipAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembershipInfo(
                Guid.NewGuid(), tenantId, "Test Tenant", "test-tenant",
                Guid.NewGuid(), Permissions.TenantRoles.Owner, (int)RestaurantSaaS.Domain.Enums.MembershipStatus.Active));

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        return new StaffOrderService(
            db,
            tenantContextMock.Object,
            currentUserMock.Object,
            membershipMock.Object,
            dateTimeProvider.Object);
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

    private static async Task SeedTestDataAsync(ApplicationDbContext db, Tenant tenant, string sessionHash)
    {
        db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenant.Id, "Concurrency Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        db.Restaurants.Add(restaurant);

        var plan = SubscriptionPlan.Create(
            $"concurrency-{tenant.Id:N}",
            $"Concurrency Plan {tenant.Id:N}",
            Money.Zero(),
            BillingInterval.Monthly,
            maxStaff: 5,
            maxMenuItems: 100,
            maxOrdersPerMonth: 500);
        db.SubscriptionPlans.Add(plan);

        var subscription = Subscription.Start(
            tenant.Id,
            plan.Id,
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
}
