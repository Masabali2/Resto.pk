using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Api.Tests;

public class StaffOrdersControllerTests
{
    private readonly Mock<IStaffOrderService> _orderService = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public StaffOrdersControllerTests()
    {
    }

    private StaffOrdersController CreateController(bool authenticated = true, Guid? userId = null)
    {
        _currentUser.Setup(c => c.IsAuthenticated).Returns(authenticated);
        _currentUser.Setup(c => c.UserId).Returns(userId);
        return new StaffOrdersController(_orderService.Object, _currentUser.Object);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(StaffOrdersController);
        var authorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        authorize.Should().NotBeEmpty();
    }

    [Fact]
    public void List_Endpoint_HasOrderViewPolicy()
    {
        var method = typeof(StaffOrdersController).GetMethod("List")!;
        var policies = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain(p => p.Contains(Permissions.OrderView));
    }

    [Fact]
    public void GetById_Endpoint_HasOrderViewPolicy()
    {
        var method = typeof(StaffOrdersController).GetMethod("GetById")!;
        var policies = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain(p => p.Contains(Permissions.OrderView));
    }

    [Fact]
    public void TransitionStatus_Endpoint_HasOrderUpdatePolicy()
    {
        var method = typeof(StaffOrdersController).GetMethod("TransitionStatus")!;
        var policies = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain(p => p.Contains(Permissions.OrderUpdate));
    }

    [Fact]
    public async Task List_WhenValid_ReturnsPagedResult()
    {
        var restaurantId = Guid.NewGuid();
        var expectedResult = new PagedResult<StaffOrderDto>
        {
            Items = new List<StaffOrderDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _orderService
            .Setup(s => s.ListAsync(restaurantId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var controller = CreateController(authenticated: true, userId: Guid.NewGuid());

        var result = await controller.List(restaurantId, null, null, 1, 20, CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PagedResult<StaffOrderDto>>;
        envelope!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WhenOrderNotFound_Returns404()
    {
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _orderService
            .Setup(s => s.GetByIdAsync(restaurantId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffOrderDto?)null);

        var controller = CreateController(authenticated: true, userId: Guid.NewGuid());

        var result = await controller.GetById(restaurantId, orderId, CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetById_WhenValid_ReturnsOrder()
    {
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var expectedOrder = new StaffOrderDto(
            orderId, "ORD-001", OrderStatus.New, OrderType.Pickup,
            "John Doe", "+923001234567", "john@example.com",
            900m, 0m, 144m, 1044m, "PKR", null,
            DateTime.UtcNow, null, null,
            new List<StaffOrderItemDto>());

        _orderService
            .Setup(s => s.GetByIdAsync(restaurantId, orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var controller = CreateController(authenticated: true, userId: Guid.NewGuid());

        var result = await controller.GetById(restaurantId, orderId, CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<StaffOrderDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.Id.Should().Be(orderId);
    }

    [Fact]
    public async Task List_WithSearch_FiltersResults()
    {
        var restaurantId = Guid.NewGuid();
        var expectedResult = new PagedResult<StaffOrderDto>
        {
            Items = new List<StaffOrderDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _orderService
            .Setup(s => s.ListAsync(restaurantId, null, "ORD-001", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var controller = CreateController(authenticated: true, userId: Guid.NewGuid());

        var result = await controller.List(restaurantId, null, "ORD-001", 1, 20, CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PagedResult<StaffOrderDto>>;
        envelope!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionStatus_WhenValid_ReturnsUpdatedOrder()
    {
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var expectedOrder = new StaffOrderDto(
            orderId, "ORD-001", OrderStatus.Confirmed, OrderType.Pickup,
            "John Doe", null, null,
            900m, 0m, 144m, 1044m, "PKR", null,
            DateTime.UtcNow, null, null,
            new List<StaffOrderItemDto>());

        _orderService
            .Setup(s => s.TransitionStatusAsync(restaurantId, orderId, It.IsAny<UpdateOrderStatusRequest>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var controller = CreateController(authenticated: true, userId: userId);

        var result = await controller.TransitionStatus(
            restaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Confirmed, null),
            CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<StaffOrderDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task TransitionStatus_WhenServiceThrowsNotFound_Throws()
    {
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _orderService
            .Setup(s => s.TransitionStatusAsync(restaurantId, orderId, It.IsAny<UpdateOrderStatusRequest>(), userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Order", orderId));

        var controller = CreateController(authenticated: true, userId: userId);

        var act = async () => await controller.TransitionStatus(
            restaurantId,
            orderId,
            new UpdateOrderStatusRequest(OrderStatus.Confirmed, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Create_Endpoint_HasOrderCreatePolicy()
    {
        var method = typeof(StaffOrdersController).GetMethod(
            "Create",
            [typeof(Guid), typeof(RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest), typeof(CancellationToken)])!;

        var policies = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain(p => p.Contains(Permissions.OrderCreate));
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreatedOrder()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var request = new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
            OrderType.Pickup,
            "John Doe",
            "+923001234567",
            "john@example.com",
            null,
            new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(Guid.NewGuid(), 1) });

        var expectedOrder = new StaffOrderDto(
            orderId, "ORD-001", OrderStatus.New, OrderType.Pickup,
            "John Doe", "+923001234567", "john@example.com",
            450m, 0m, 72m, 522m, "PKR", "No onions",
            DateTime.UtcNow, null, null,
            new List<StaffOrderItemDto>());

        _orderService
            .Setup(s => s.CreateAsync(restaurantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var controller = CreateController(authenticated: true, userId: userId);

        var result = await controller.Create(restaurantId, request, CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<StaffOrderDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.Id.Should().Be(orderId);
        envelope.Data.Status.Should().Be(OrderStatus.New);
        envelope.Data.TotalAmount.Should().Be(522m);
    }

    [Fact]
    public async Task Create_WhenServiceThrowsNotFound_Returns404()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest(
            OrderType.Pickup,
            "John Doe",
            null,
            null,
            null,
            new[] { new RestaurantSaaS.Application.Features.Orders.DTOs.OrderItemRequest(Guid.NewGuid(), 1) });

        _orderService
            .Setup(s => s.CreateAsync(restaurantId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Restaurant", restaurantId));

        var controller = CreateController(authenticated: true, userId: userId);

        var act = async () => await controller.Create(restaurantId, request, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
