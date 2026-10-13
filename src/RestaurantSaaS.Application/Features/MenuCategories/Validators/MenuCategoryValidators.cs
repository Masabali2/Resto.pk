using FluentValidation;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;

namespace RestaurantSaaS.Application.Features.MenuCategories.Validators;

public sealed class CreateMenuCategoryRequestValidator : AbstractValidator<CreateMenuCategoryRequest>
{
    public CreateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue);
    }
}

public sealed class UpdateMenuCategoryRequestValidator : AbstractValidator<UpdateMenuCategoryRequest>
{
    public UpdateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue);
    }
}

public sealed class UpdateMenuCategoryStatusRequestValidator : AbstractValidator<UpdateMenuCategoryStatusRequest>
{
    public UpdateMenuCategoryStatusRequestValidator()
    {
        RuleFor(x => x.IsActive).NotNull();
    }
}

public sealed class ReorderMenuCategoriesRequestValidator : AbstractValidator<ReorderMenuCategoriesRequest>
{
    public ReorderMenuCategoriesRequestValidator()
    {
        RuleFor(x => x.OrderedCategoryIds)
            .NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Category ids must be unique.");
    }
}
