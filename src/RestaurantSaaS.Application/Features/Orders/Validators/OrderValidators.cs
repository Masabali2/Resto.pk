using FluentValidation;
using RestaurantSaaS.Application.Features.Orders.DTOs;

namespace RestaurantSaaS.Application.Features.Orders.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
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
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one order item is required.");
        RuleFor(x => x.Items)
            .Must(items =>
            {
                if (items is null)
                    return false;
                if (items.Any(i => i is null))
                    return false;
                var ids = items.Select(i => i!.MenuItemId);
                return ids.Distinct().Count() == ids.Count();
            })
            .WithMessage("Duplicate menu item IDs are not allowed in the same order.");
        RuleForEach(x => x.Items).SetValidator(new OrderItemRequestValidator());
    }
}

public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.MenuItemId).NotEmpty().WithMessage("Menu item ID is required.");
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 999)
            .WithMessage("Quantity must be between 1 and 999.");
    }
}

public sealed class OrderListQueryValidator : AbstractValidator<OrderListQuery>
{
    public OrderListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        RuleFor(x => x.Search).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Search));
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.OrderType).IsInEnum().When(x => x.OrderType.HasValue);
        RuleFor(x => x.PaymentStatus).IsInEnum().When(x => x.PaymentStatus.HasValue);
        RuleFor(x => x)
            .Must(x => x.DateFrom is null || x.DateTo is null || x.DateFrom <= x.DateTo)
            .WithMessage("DateFrom must be on or before DateTo.");
    }
}

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

public sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
