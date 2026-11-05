using FluentValidation;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;

namespace RestaurantSaaS.Application.Features.PublicOrders.Validators;

public sealed class CreatePublicOrderRequestValidator : AbstractValidator<CreatePublicOrderRequest>
{
    public CreatePublicOrderRequestValidator()
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
