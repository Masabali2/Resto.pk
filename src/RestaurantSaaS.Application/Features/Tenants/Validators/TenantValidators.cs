using FluentValidation;
using RestaurantSaaS.Application.Features.Tenants.DTOs;

namespace RestaurantSaaS.Application.Features.Tenants.Validators;

public sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9-]{1,61}[a-zA-Z0-9]$")
            .WithMessage("Slug must be 3-63 characters, letters/numbers/hyphens, and cannot start or end with a hyphen.");
    }
}

public sealed class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateTenantStatusRequestValidator : AbstractValidator<UpdateTenantStatusRequest>
{
    private static readonly string[] Allowed = ["Active", "Suspended", "Cancelled"];

    public UpdateTenantStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => Allowed.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", Allowed)}.");
    }
}
