using FluentValidation;
using RestaurantSaaS.Application.Features.Menus.DTOs;

namespace RestaurantSaaS.Application.Features.Menus.Validators;

public sealed class CreateMenuRequestValidator : AbstractValidator<CreateMenuRequest>
{
   public CreateMenuRequestValidator()
   {
       RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
       RuleFor(x => x.Description).MaximumLength(1000);
   }
}

public sealed class UpdateMenuRequestValidator : AbstractValidator<UpdateMenuRequest>
{
   public UpdateMenuRequestValidator()
   {
       RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
       RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
   }
}

public sealed class UpdateMenuStatusRequestValidator : AbstractValidator<UpdateMenuStatusRequest>
{
   public UpdateMenuStatusRequestValidator()
   {
       RuleFor(x => x.IsActive).NotNull();
   }
}
