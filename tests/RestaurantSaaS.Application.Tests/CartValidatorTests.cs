using FluentAssertions;
using RestaurantSaaS.Application.Features.Carts.DTOs;
using RestaurantSaaS.Application.Features.Carts.Validators;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Tests;

public class CartValidatorTests
{
    private readonly AddCartItemRequestValidator _addItem = new();
    private readonly UpdateCartItemRequestValidator _updateItem = new();
    private readonly CheckoutCartRequestValidator _checkout = new();

    private static AddCartItemRequest ValidAddItem(Guid? menuItemId = null, int quantity = 2) =>
        new(menuItemId ?? Guid.NewGuid(), quantity);

    private static CheckoutCartRequest ValidCheckout() =>
        new(
            OrderType.Pickup,
            "Jane Doe",
            "+923001234567",
            "jane@example.com",
            "Extra napkins");

    [Fact]
    public void AddCartItem_WhenValid_Passes()
    {
        var result = _addItem.Validate(ValidAddItem());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddCartItem_WhenEmptyMenuItemId_Fails()
    {
        var result = _addItem.Validate(new AddCartItemRequest(Guid.Empty, 2));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddCartItemRequest.MenuItemId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void AddCartItem_WhenInvalidQuantity_Fails(int quantity)
    {
        var result = _addItem.Validate(new AddCartItemRequest(Guid.NewGuid(), quantity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AddCartItemRequest.Quantity));
    }

    [Fact]
    public void UpdateCartItem_WhenValidQuantity_Passes()
    {
        var result = _updateItem.Validate(new UpdateCartItemRequest(5));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void UpdateCartItem_WhenInvalidQuantity_Fails(int quantity)
    {
        var result = _updateItem.Validate(new UpdateCartItemRequest(quantity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCartItemRequest.Quantity));
    }

    [Fact]
    public void Checkout_WhenValid_Passes()
    {
        var result = _checkout.Validate(ValidCheckout());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Checkout_WhenMissingCustomerName_Fails()
    {
        var request = ValidCheckout() with { CustomerName = "" };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.CustomerName));
    }

    [Fact]
    public void Checkout_WhenCustomerNameTooLong_Fails()
    {
        var request = ValidCheckout() with { CustomerName = new string('a', 151) };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.CustomerName));
    }

    [Fact]
    public void Checkout_WhenInvalidPhone_Fails()
    {
        var request = ValidCheckout() with { CustomerPhone = "invalid" };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.CustomerPhone));
    }

    [Fact]
    public void Checkout_WhenInvalidEmail_Fails()
    {
        var request = ValidCheckout() with { CustomerEmail = "invalid" };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.CustomerEmail));
    }

    [Fact]
    public void Checkout_WhenInvalidOrderType_Fails()
    {
        var request = ValidCheckout() with { OrderType = (OrderType)99 };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.OrderType));
    }

    [Fact]
    public void Checkout_WhenSpecialInstructionsTooLong_Fails()
    {
        var request = ValidCheckout() with { SpecialInstructions = new string('x', 2001) };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CheckoutCartRequest.SpecialInstructions));
    }

    [Fact]
    public void Checkout_WhenOptionalFieldsNull_Passes()
    {
        var request = ValidCheckout() with { CustomerPhone = null, CustomerEmail = null, SpecialInstructions = null };

        var result = _checkout.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
