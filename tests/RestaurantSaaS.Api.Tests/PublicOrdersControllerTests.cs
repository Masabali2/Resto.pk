using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Api.Tests;

public class PublicOrdersControllerTests
{
    private readonly Mock<IPublicRestaurantService> _restaurants = new();
    private readonly Mock<IPublicOrderService> _orderService = new();

    public PublicOrdersControllerTests()
    {
    }

    private PublicOrdersController CreateController() =>
        new(_restaurants.Object, _orderService.Object);

    [Fact]
    public async Task Create_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.Create(
            "invalid-slug",
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null),
            CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
        var envelope = objectResult.Value as ApiResponse<CreatePublicOrderResponse>;
        envelope!.Success.Should().BeFalse();
        envelope.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsOrderResponse()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedResponse = new CreatePublicOrderResponse(
            Guid.NewGuid(), "ORD-123", OrderStatus.New, OrderType.Pickup, 1044m, "PKR",
            "raw-token-123",
            new List<PublicOrderItemDto>(), DateTime.UtcNow);

        _orderService
            .Setup(s => s.CreateOrderFromCartAsync(restaurant.Id, It.IsAny<CreatePublicOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = CreateController();

        var result = await controller.Create(
            "test-slug",
            new CreatePublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null),
            CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<CreatePublicOrderResponse>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.OrderNumber.Should().Be("ORD-123");
        envelope.Data.TotalAmount.Should().Be(1044m);
    }

    [Fact]
    public async Task CreateDirect_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.CreateDirect(
            "invalid-slug",
            new CreateDirectPublicOrderRequest(OrderType.Pickup, "John Doe", null, null, null, new List<DirectPublicOrderItemRequest>()),
            CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateDirect_WhenValid_ReturnsOrderResponse()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedResponse = new CreatePublicOrderResponse(
            Guid.NewGuid(), "ORD-DIRECT-123", OrderStatus.New, OrderType.Delivery, 1044m, "PKR",
            "raw-direct-token-456",
            new List<PublicOrderItemDto>(), DateTime.UtcNow);

        _orderService
            .Setup(s => s.CreateDirectOrderAsync(restaurant.Id, It.IsAny<CreateDirectPublicOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = CreateController();

        var result = await controller.CreateDirect(
            "test-slug",
            new CreateDirectPublicOrderRequest(
                OrderType.Delivery, "John Doe", "+923001234567", "john@example.com", null,
                new List<DirectPublicOrderItemRequest> { new(Guid.NewGuid(), 2) }),
            CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<CreatePublicOrderResponse>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.OrderNumber.Should().Be("ORD-DIRECT-123");
        envelope.Data.TotalAmount.Should().Be(1044m);
        _orderService.Verify(
            s => s.CreateDirectOrderAsync(restaurant.Id, It.IsAny<CreateDirectPublicOrderRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Track_WhenOrderNotFound_ReturnsNotFound()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        _orderService
            .Setup(s => s.GetOrderByTrackingTokenAsync(restaurant.Id, "invalid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderTrackingDto?)null);

        var controller = CreateController();

        var result = await controller.Track("test-slug", "invalid-token", CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Track_WhenValidToken_ReturnsTrackingDto()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedDto = new PublicOrderTrackingDto(
            "ORD-123", OrderStatus.Preparing, 1044m, "PKR",
            new List<PublicTrackingOrderItemDto> { new("Burger", 2) },
            DateTime.UtcNow, null);

        _orderService
            .Setup(s => s.GetOrderByTrackingTokenAsync(restaurant.Id, "valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var controller = CreateController();

        var result = await controller.Track("test-slug", "valid-token", CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PublicOrderTrackingDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.OrderNumber.Should().Be("ORD-123");
        envelope.Data.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public void Controller_HasAllowAnonymousAttribute()
    {
        var controllerType = typeof(PublicOrdersController);
        var allowAnonymous = controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
        allowAnonymous.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_HasAutoValidateAntiforgeryTokenAttribute()
    {
        var controllerType = typeof(PublicOrdersController);
        var antiforgery = controllerType.GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true);
        antiforgery.Should().NotBeEmpty();
    }
}
