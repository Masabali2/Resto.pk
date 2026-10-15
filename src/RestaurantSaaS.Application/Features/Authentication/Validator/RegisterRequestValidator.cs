
using FluentValidation;
using RestaurantSaaS.Application.Features.Authentication.DTOs;

namespace RestaurantSaaS.Application.Features.Authentication.Validator;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(r => r.UserName)
            .NotEmpty()
            .Length(3, 50)
            .Matches(@"^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username may contain letters, numbers, periods, underscores, and hyphens.");

        RuleFor(r => r.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]")
            .WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]")
            .WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]")
            .WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must contain a non-alphanumeric character.");

        RuleFor(r => r.ConfirmPassword)
            .NotEmpty()
            .Equal(r => r.Password)
            .WithMessage("Password and confirmation password do not match.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.EmailOrUserName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}