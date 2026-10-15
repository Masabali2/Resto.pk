using FluentValidation;
using RestaurantSaaS.Application.Features.Authentication.DTOs;

namespace RestaurantSaaS.Application.Features.Authentication.Validators;

public sealed class ForgotPasswordRequestValidator
    : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("A valid email address is required.")
            .MaximumLength(256);
    }
}