using FluentValidation;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Subscriptions.Validators;

public sealed class CreateSubscriptionPlanRequestValidator : AbstractValidator<CreateSubscriptionPlanRequest>
{
    public CreateSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency)
            .Must(c => !string.IsNullOrWhiteSpace(c) && c.Length == 3)
            .WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.BillingInterval)
            .Must(v => Enum.TryParse<BillingInterval>(v, ignoreCase: false, out _))
            .WithMessage($"BillingInterval must be one of: {string.Join(", ", Enum.GetNames<BillingInterval>())}.");
        RuleFor(x => x.MaxStaff).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxMenuItems).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxOrdersPerMonth).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSubscriptionPlanLimitsRequestValidator : AbstractValidator<UpdateSubscriptionPlanLimitsRequest>
{
    public UpdateSubscriptionPlanLimitsRequestValidator()
    {
        RuleFor(x => x.MaxStaff).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxMenuItems).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxOrdersPerMonth).GreaterThanOrEqualTo(0);
    }
}

public sealed class StartTenantSubscriptionRequestValidator : AbstractValidator<StartTenantSubscriptionRequest>
{
    public StartTenantSubscriptionRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEqual(Guid.Empty);
    }
}

public sealed class ChangeTenantSubscriptionPlanRequestValidator : AbstractValidator<ChangeTenantSubscriptionPlanRequest>
{
    public ChangeTenantSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.NewPlanId).NotEqual(Guid.Empty);
    }
}

public sealed class SetSubscriptionStatusRequestValidator : AbstractValidator<SetSubscriptionStatusRequest>
{
    public SetSubscriptionStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(v => Enum.TryParse<SubscriptionStatus>(v, ignoreCase: false, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<SubscriptionStatus>())}.");
    }
}

public sealed class SetAutoRenewRequestValidator : AbstractValidator<SetAutoRenewRequest>
{
    public SetAutoRenewRequestValidator()
    {
    }
}
