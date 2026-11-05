using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.PublicCarts;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Api.Tests;

public class PublicCartControllerTests
{
    private readonly Mock<IPublicRestaurantService> _restaurants = new();
    private readonly Mock<IPublicCartService> _cartService = new();

    public PublicCartControllerTests()
    {
    }

    private PublicCartController CreateController() =>
        new(_restaurants.Object, _cartService.Object);

    [Fact]
    public async Task Get_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.Get("invalid-slug", CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Get_WhenValid_ReturnsCart()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedCart = new PublicCartDto(
            Guid.NewGuid(), restaurant.Id, 1, 450m, "PKR",
            DateTime.UtcNow.AddHours(24),
            new List<PublicCartItemDto>());

        _cartService
            .Setup(s => s.GetCurrentCartAsync(restaurant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCart);

        var controller = CreateController();

        var result = await controller.Get("test-slug", CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PublicCartDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.TotalAmount.Should().Be(450m);
    }

    [Fact]
    public async Task AddItem_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.AddItem(
            "invalid-slug",
            new AddPublicCartItemRequest(Guid.NewGuid(), 1),
            CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task AddItem_WhenValid_ReturnsCartItem()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedItem = new PublicCartItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Test Burger", 450m, 2, "PKR", 900m);

        _cartService
            .Setup(s => s.AddItemAsync(restaurant.Id, It.IsAny<AddPublicCartItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        var controller = CreateController();

        var result = await controller.AddItem(
            "test-slug",
            new AddPublicCartItemRequest(Guid.NewGuid(), 2),
            CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PublicCartItemDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.ItemName.Should().Be("Test Burger");
        envelope.Data.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task UpdateItem_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.UpdateItem(
            "invalid-slug",
            Guid.NewGuid(),
            new UpdatePublicCartItemRequest(1),
            CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateItem_WhenValid_ReturnsUpdatedItem()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        var expectedItem = new PublicCartItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "Test Burger", 450m, 3, "PKR", 1350m);

        _cartService
            .Setup(s => s.UpdateItemAsync(restaurant.Id, It.IsAny<Guid>(), It.IsAny<UpdatePublicCartItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        var controller = CreateController();

        var result = await controller.UpdateItem(
            "test-slug",
            Guid.NewGuid(),
            new UpdatePublicCartItemRequest(3),
            CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
        var envelope = ok.Value as ApiResponse<PublicCartItemDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.Quantity.Should().Be(3);
        envelope.Data.LineTotal.Should().Be(1350m);
    }

    [Fact]
    public async Task RemoveItem_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.RemoveItem("invalid-slug", Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RemoveItem_WhenValid_ReturnsOk()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        _cartService
            .Setup(s => s.RemoveItemAsync(restaurant.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController();

        var result = await controller.RemoveItem("test-slug", Guid.NewGuid(), CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Clear_WhenRestaurantNotFound_ReturnsNotFound()
    {
        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("invalid-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicOrderingRestaurantDto?)null);

        var controller = CreateController();

        var result = await controller.Clear("invalid-slug", CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Clear_WhenValid_ReturnsOk()
    {
        var restaurant = new PublicOrderingRestaurantDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Restaurant",
            "test-slug", "Karachi", "Pakistan", "UTC", 16m);

        _restaurants
            .Setup(r => r.GetValidRestaurantForOrderingAsync("test-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        _cartService
            .Setup(s => s.ClearAsync(restaurant.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController();

        var result = await controller.Clear("test-slug", CancellationToken.None);

        var ok = result.Result as ObjectResult;
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void Controller_HasAllowAnonymousAttribute()
    {
        var controllerType = typeof(PublicCartController);
        var allowAnonymous = controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
        allowAnonymous.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_HasAutoValidateAntiforgeryTokenAttribute()
    {
        var controllerType = typeof(PublicCartController);
        var antiforgery = controllerType.GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true);
        antiforgery.Should().NotBeEmpty();
    }
}
