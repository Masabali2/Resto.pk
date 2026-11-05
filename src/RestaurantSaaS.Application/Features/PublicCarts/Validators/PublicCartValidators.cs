using FluentValidation;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;

namespace RestaurantSaaS.Application.Features.PublicCarts.Validators;

public sealed class AddPublicCartItemRequestValidator : AbstractValidator<AddPublicCartItemRequest>
{
    public AddPublicCartItemRequestValidator()
    {
        RuleFor(x => x.MenuItemId).NotEmpty().WithMessage("Menu item ID is required.");
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99.");
    }
}

public sealed class UpdatePublicCartItemRequestValidator : AbstractValidator<UpdatePublicCartItemRequest>
{
    public UpdatePublicCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99.");
    }
}
