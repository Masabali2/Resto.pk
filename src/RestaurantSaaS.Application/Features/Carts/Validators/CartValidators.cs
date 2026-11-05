using FluentValidation;
using RestaurantSaaS.Application.Features.Carts.DTOs;

namespace RestaurantSaaS.Application.Features.Carts.Validators;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.MenuItemId).NotEmpty().WithMessage("Menu item ID is required.");
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99.");
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99.");
    }
}

public sealed class CheckoutCartRequestValidator : AbstractValidator<CheckoutCartRequest>
{
    public CheckoutCartRequestValidator()
    {
        RuleFor(x => x.OrderType).IsInEnum();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CustomerPhone)
            .MaximumLength(30)
            .Matches(@"^\+?[0-9\s\-()]{7,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerPhone));
        RuleFor(x => x.CustomerEmail)
            .EmailAddress()
            .MaximumLength(254)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.SpecialInstructions)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.SpecialInstructions));
    }
}
