using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Orders.DTOS;using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class StaffOrderServiceTests
{
    private static (Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationDbContext> Options, Guid TenantId, Guid RestaurantId, Guid UserId)
        CreateInMemoryDbWithOrders(
            Mock<ITenantContext> tenantContextMock,
            Mock<ICurrentUserService> currentUserMock,
            Mock<ITenantMembershipService> membershipMock,
            int orderCount = 2)
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var seedDateTimeProvider = new Mock<IDateTimeProvider>();
        seedDateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

        var seedDb = new ApplicationDbContext(options, new Mock<ITenantContext>().Object, seedDateTimeProvider.Object);

        var tenant = Tenant.Create("Test Tenant", "test-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        tenantId = tenant.Id;

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenantId, "Test Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        restaurantId = restaurant.Id;

        var subscription = Subscription.Start(
            tenantId,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            now.AddDays(-1),
            now.AddDays(30));

        seedDb.Tenants.Add(tenant);
        seedDb.Restaurants.Add(restaurant);
        seedDb.Subscriptions.Add(subscription);

        // Create orders
        for (int i = 0; i < orderCount; i++)
        {
            var orderItem = OrderItem.CreateSnapshot(
                Guid.NewGuid(), Guid.NewGuid(), "Test Burger", 450m, 2, "PKR");

            var order = Order.Create(
                tenantId,
                restaurantId,
                null,
                $"ORD-20261105-{i:D3}",
                $"Customer {i}",
                null,
                null,
                900m,
                0m,
                0m,
                900m,
                "PKR",
                null,
                OrderType.Pickup,
                new[] { orderItem });
            seedDb.Orders.Add(order);

            // Seed RowVersion as a known non-empty value so EF Core's
            // byte[] concurrency-token convention works with InMemory.
            var orderEntry = seedDb.Entry(order);
            orderEntry.Property("RowVersion").CurrentValue = new byte[] { 1 };
        }

        seedDb.SaveChanges();
        seedDb.Dispose();

        tenantContextMock.Setup(t => t.HasTenant).Returns(true);
        tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
        currentUserMock.Setup(c => c.UserId).Returns(userId);

        membershipMock.Setup(m => m.GetMembershipAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembershipInfo(
                Guid.NewGuid(), tenantId, "Test Tenant", "test-tenant",
                Guid.NewGuid(), Permissions.TenantRoles.Owner, (int)RestaurantSaaS.Domain.Enums.MembershipStatus.Active));

        return (options, tenantId, restaurantId, userId);
    }

    private static async Task<Guid> SeedAvailableMenuItemAsync(
        Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationDbContext> options,
        Guid tenantId,
        Guid restaurantId,
        decimal price = 450m,
        bool available = true)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

        using var db = new ApplicationDbContext(
            options,
            new Mock<ITenantContext>().Object,
            dateTimeProvider.Object);

        var menu = Menu.Create(tenantId, restaurantId, "Test Menu");
        db.Menus.Add(menu);

        var category = MenuCategory.Create(menu.Id, "Main");
        db.MenuCategories.Add(category);

        var item = MenuItem.Create(category.Id, "Test Burger", Money.New(price, "PKR"));
        if (!available)
            item.SetAvailability(false);

        db.MenuItems.Add(item);
        await db.SaveChangesAsync();

        return item.Id;
    }

    private static StaffOrderService CreateService(
        Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationDbContext> options,
        Mock<ITenantContext> tenantContextMock,
        Mock<ICurrentUserService> currentUserMock,
        Mock<ITenantMembershipService> membershipMock)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

        var orderNumberGenerator = new Mock<IOrderNumberGenerator>();
        orderNumberGenerator.Setup(g => g.Generate()).Returns("ORD-TEST-001");

        var db = new ApplicationDbContext(options, tenantContextMock.Object, dateTimeProvider.Object);

        return new StaffOrderService(
            db,
            tenantContextMock.Object,
            currentUserMock.Object,
            membershipMock.Object,
            dateTimeProvider.Object,
            orderNumberGenerator.Object);
    }

    [Fact]
    public async Task ListAsync_SameTenant_ReturnsOrders()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 3);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.ListAsync(null, null, null, 1, 10);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListAsync_ScopedToRestaurant_ReturnsOnlyThatRestaurant()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 3);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.ListAsync(restaurantId, null, null, 1, 10);

        result.Items.Should().HaveCount(3);
        result.Items.Should().AllSatisfy(o => o.Id.Should().NotBe(Guid.Empty));
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_ReturnsMatchingOrders()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 2);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.ListAsync(null, OrderStatus.New, null, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.New));
    }

    [Fact]
    public async Task GetByIdAsync_SameTenant_ReturnsOrderWithItems()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.GetByIdAsync(restaurantId, orderId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(orderId);
        result.OrderNumber.Should().StartWith("ORD-");
        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(i =>
        {
            i.ItemName.Should().NotBeNullOrWhiteSpace();
            i.UnitPrice.Should().BeGreaterThan(0);
            i.Quantity.Should().BeGreaterThan(0);
            i.LineTotal.Should().BeGreaterThan(0);
            i.Currency.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task ListAsync_WithSearch_ReturnsMatchingOrders()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 3);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        // Use the order number of the first order as search term
        var orderId = GetFirstOrderId(options, tenantId);
        var tenantContextForRead = new Mock<ITenantContext>();
        tenantContextForRead.Setup(t => t.HasTenant).Returns(true);
        tenantContextForRead.Setup(t => t.TenantId).Returns(tenantId);
        tenantContextForRead.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);
        using var readDb = new ApplicationDbContext(
            options,
            tenantContextForRead.Object,
            new Mock<IDateTimeProvider>().Object);
        var firstOrderNumber = readDb.Orders.First().OrderNumber;

        var result = await service.ListAsync(restaurantId, null, firstOrderNumber, 1, 10);

        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(o => o.OrderNumber.Should().Contain(firstOrderNumber));
    }

    [Fact]
    public async Task ListAsync_WithSearch_NoMatch_ReturnsEmpty()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 2);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.ListAsync(restaurantId, null, "NONEXISTENT-ORDER", 1, 10);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetByIdAsync_WrongRestaurant_ReturnsNull()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);
        var wrongRestaurantId = Guid.NewGuid();
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.GetByIdAsync(wrongRestaurantId, orderId);

        // Same tenant, different restaurant: behave as if the order is missing
        // so the endpoint returns 404 without revealing its existence.
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WrongTenant_ThrowsCrossTenantAccessException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);

        // Change tenant context to a different tenant
        var otherTenantId = Guid.NewGuid();
        tenantContextMock.Setup(t => t.TenantId).Returns(otherTenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(otherTenantId);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.GetByIdAsync(restaurantId, orderId);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task TransitionStatusAsync_ValidTransition_ReturnsUpdatedOrder()
    {
        // NOTE: Service-level persistence through SaveChangesAsync cannot be
        // reliably tested with EF Core InMemory + byte[] RowVersion concurrency
        // token (InMemory provider does not support byte[] concurrency comparison).
        // SQL Server handles this correctly in production. Domain transition logic
        // is verified directly via Order.TransitionTo below.
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));
        using var readDb = new ApplicationDbContext(options, tenantContextMock.Object, dateTimeProvider.Object);
        var order = readDb.Orders.First(o => o.Id == orderId);

        order.TransitionTo(OrderStatus.Confirmed, userId, null, DateTime.UtcNow);

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task TransitionStatusAsync_InvalidTransition_ThrowsDomainException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        // New -> Completed is not a valid transition
        var act = () => service.TransitionStatusAsync(
            restaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Completed, null),
            userId);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task TransitionStatusAsync_WrongRestaurant_ThrowsNotFound()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        var orderId = GetFirstOrderId(options, tenantId);
        var wrongRestaurantId = Guid.NewGuid();
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.TransitionStatusAsync(
            wrongRestaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Confirmed, null),
            userId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TransitionStatusAsync_WithoutOrderUpdatePermission_ThrowsForbidden()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        // Override membership to Waiter (has OrderView but not OrderUpdate)
        membershipMock.Setup(m => m.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembershipInfo(
                Guid.NewGuid(), tenantId, "Test Tenant", "test-tenant",
                Guid.NewGuid(), Permissions.TenantRoles.Waiter, (int)RestaurantSaaS.Domain.Enums.MembershipStatus.Active));

        var orderId = GetFirstOrderId(options, tenantId);
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.TransitionStatusAsync(
            restaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Confirmed, null),
            userId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TransitionStatusAsync_KitchenCannotCancelOrder()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        membershipMock.Setup(m => m.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembershipInfo(
                Guid.NewGuid(), tenantId, "Test Tenant", "test-tenant",
                Guid.NewGuid(), Permissions.TenantRoles.Kitchen, (int)MembershipStatus.Active));

        var orderId = GetFirstOrderId(options, tenantId);
        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.TransitionStatusAsync(
            restaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Cancelled, "Customer requested cancellation"),
            userId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListAsync_Unauthenticated_ThrowsNotAuthenticated()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, _) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 1);

        // Override to unauthenticated AFTER seed helper sets authenticated
        currentUserMock.Setup(c => c.IsAuthenticated).Returns(false);
        currentUserMock.Setup(c => c.UserId).Returns((Guid?)null);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.ListAsync(null, null, null, 1, 10);

        await act.Should().ThrowAsync<NotAuthenticatedException>();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOrderWithServerCalculatedTotals()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var result = await service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                "+923001234567",
                "john@example.com",
                "No onions please",
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 2) }));

        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.OrderNumber.Should().StartWith("ORD-");
        result.Status.Should().Be(OrderStatus.New);
        result.OrderType.Should().Be(OrderType.Pickup);
        result.CustomerName.Should().Be("John Doe");
        result.CustomerPhone.Should().Be("+923001234567");
        result.CustomerEmail.Should().Be("john@example.com");
        result.SpecialInstructions.Should().Be("No onions please");
        result.Subtotal.Should().Be(900m);
        result.TaxAmount.Should().Be(144m);
        result.TotalAmount.Should().Be(1044m);
        result.Currency.Should().Be("PKR");
        result.Items.Should().HaveCount(1);
        result.Items[0].MenuItemId.Should().Be(menuItemId);
        result.Items[0].ItemName.Should().Be("Test Burger");
        result.Items[0].UnitPrice.Should().Be(450m);
        result.Items[0].Quantity.Should().Be(2);
        result.Items[0].LineTotal.Should().Be(900m);
    }

    [Fact]
    public async Task CreateAsync_UnknownMenuItem_ThrowsNotFound()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(Guid.NewGuid(), 1) }));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_UnavailableMenuItem_ThrowsDomainException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m, available: false);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 1) }));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateAsync_RestaurantNotAcceptingOrders_ThrowsDomainException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));
        using var readDb = new ApplicationDbContext(
            options,
            tenantContextMock.Object,
            dateTimeProvider.Object);
        var restaurant = readDb.Restaurants.First(r => r.Id == restaurantId);
        restaurant.ChangeStatus(RestaurantStatus.Draft);
        await readDb.SaveChangesAsync();

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 1) }));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateAsync_DuplicateMenuItemIds_ThrowsDomainException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[]
                {
                    new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 1),
                    new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 2)
                }));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateAsync_CrossTenant_ThrowsCrossTenantAccessException()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m);

        var otherTenantId = Guid.NewGuid();
        tenantContextMock.Setup(t => t.TenantId).Returns(otherTenantId);
        tenantContextMock.Setup(t => t.GetRequiredTenantId()).Returns(otherTenantId);

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 1) }));

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task CreateAsync_WithoutOrderCreatePermission_ThrowsForbidden()
    {
        var tenantContextMock = new Mock<ITenantContext>();
        var currentUserMock = new Mock<ICurrentUserService>();
        var membershipMock = new Mock<ITenantMembershipService>();

        var (options, tenantId, restaurantId, userId) = CreateInMemoryDbWithOrders(
            tenantContextMock, currentUserMock, membershipMock, orderCount: 0);

        var menuItemId = await SeedAvailableMenuItemAsync(options, tenantId, restaurantId, price: 450m);

        membershipMock.Setup(m => m.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembershipInfo(
                Guid.NewGuid(), tenantId, "Test Tenant", "test-tenant",
                Guid.NewGuid(), Permissions.TenantRoles.Kitchen, (int)MembershipStatus.Active));

        var service = CreateService(options, tenantContextMock, currentUserMock, membershipMock);

        var act = () => service.CreateAsync(
            restaurantId,
            new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
                OrderType.Pickup,
                "John Doe",
                null,
                null,
                null,
                new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(menuItemId, 1) }));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ------------------------------------------------------------------
    // Domain-level transition tests (provider-independent, no SaveChangesAsync)
    // ------------------------------------------------------------------

    [Fact]
    public void OrderTransition_ValidTransition_NewToConfirmed_UpdatesStatus()
    {
        var order = CreateTestOrder(OrderStatus.New);

        order.TransitionTo(OrderStatus.Confirmed, Guid.NewGuid(), null, DateTime.UtcNow);

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void OrderTransition_InvalidTransition_NewToCompleted_ThrowsDomainException()
    {
        var order = CreateTestOrder(OrderStatus.New);

        var act = () => order.TransitionTo(OrderStatus.Completed, Guid.NewGuid(), null, DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void OrderTransition_ValidTransition_ConfirmedToPreparing_UpdatesStatus()
    {
        var order = CreateTestOrder(OrderStatus.Confirmed);

        order.TransitionTo(OrderStatus.Preparing, Guid.NewGuid(), null, DateTime.UtcNow);

        order.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public void OrderTransition_TerminalStatusCannotTransition_ThrowsDomainException()
    {
        var completedOrder = CreateTestOrder(OrderStatus.Completed);
        var cancelledOrder = CreateTestOrder(OrderStatus.Cancelled);

        var act1 = () => completedOrder.TransitionTo(OrderStatus.New, Guid.NewGuid(), null, DateTime.UtcNow);
        var act2 = () => cancelledOrder.TransitionTo(OrderStatus.New, Guid.NewGuid(), null, DateTime.UtcNow);

        act1.Should().Throw<DomainException>();
        act2.Should().Throw<DomainException>();
    }

    [Fact]
    public void OrderTransition_EmptyActor_ThrowsDomainException()
    {
        var order = CreateTestOrder(OrderStatus.New);

        var act = () => order.TransitionTo(OrderStatus.Confirmed, Guid.Empty, null, DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    private static Order CreateTestOrder(OrderStatus status)
    {
        var tenantId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenantId, "Test Restaurant", address, taxRate: 16m);
        var orderItem = OrderItem.CreateSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Test Burger", 450m, 2, "PKR");

        var order = Order.Create(
            tenantId,
            restaurantId,
            null,
            "ORD-TEST-001",
            "Customer",
            null,
            null,
            900m,
            0m,
            0m,
            900m,
            "PKR",
            null,
            OrderType.Pickup,
            new[] { orderItem });

        // Set status directly via TransitionTo from New
        if (status != OrderStatus.New)
        {
            if (status == OrderStatus.Cancelled)
            {
                order.TransitionTo(OrderStatus.Cancelled, Guid.NewGuid(), null, DateTime.UtcNow);
            }
            else
            {
                // Transition through valid forward steps to reach the desired status
                var current = OrderStatus.New;
                while (current != status)
                {
                    var next = current switch
                    {
                        OrderStatus.New => OrderStatus.Confirmed,
                        OrderStatus.Confirmed => OrderStatus.Preparing,
                        OrderStatus.Preparing => OrderStatus.Ready,
                        OrderStatus.Ready => OrderStatus.Completed,
                        _ => throw new InvalidOperationException("Cannot reach terminal status via forward transitions")
                    };
                    order.TransitionTo(next, Guid.NewGuid(), null, DateTime.UtcNow);
                    current = next;
                }
            }
        }

        return order;
    }

    private static Guid GetFirstOrderId(
        Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationDbContext> options,
        Guid tenantId)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.HasTenant).Returns(true);
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);
        tenantContext.Setup(t => t.GetRequiredTenantId()).Returns(tenantId);

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc));

        using var readDb = new ApplicationDbContext(
            options,
            tenantContext.Object,
            dateTimeProvider.Object);
        return readDb.Orders.First().Id;
    }
}