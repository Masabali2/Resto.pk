using FluentValidation;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;

namespace RestaurantSaaS.Application.Features.MenuItems.Validators;

public sealed class CreateMenuItemRequestValidator : AbstractValidator<CreateMenuItemRequest>
{
    public CreateMenuItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, false);
        RuleFor(x => x.Currency)
            .Length(3)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue);
        RuleFor(x => x.ImageFileId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Image file id is invalid.");
    }
}

public sealed class UpdateMenuItemRequestValidator : AbstractValidator<UpdateMenuItemRequest>
{
    public UpdateMenuItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, false).When(x => x.Price.HasValue);
        RuleFor(x => x.Currency)
            .Length(3)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue);
        RuleFor(x => x)
            .Must(x => !(x.ClearImage && x.ImageFileId.HasValue))
            .WithMessage("ClearImage cannot be combined with ImageFileId.");
        RuleFor(x => x.ImageFileId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Image file id is invalid.");
    }
}

public sealed class UpdateMenuItemAvailabilityRequestValidator : AbstractValidator<UpdateMenuItemAvailabilityRequest>
{
    public UpdateMenuItemAvailabilityRequestValidator()
    {
        RuleFor(x => x.IsAvailable).NotNull();
    }
}

public sealed class UpdateMenuItemStatusRequestValidator : AbstractValidator<UpdateMenuItemStatusRequest>
{
    public UpdateMenuItemStatusRequestValidator()
    {
        RuleFor(x => x.IsActive).NotNull();
    }
}
