using FluentValidation;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Application.Features.Restaurants.Validators;

public sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}

public sealed class CreateRestaurantRequestValidator : AbstractValidator<CreateRestaurantRequest>
{
    public CreateRestaurantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9\s\-()]{7,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressInputValidator());
        RuleFor(x => x.TimeZone).MaximumLength(64)
            .Must(BeValidIanaTimeZone).WithMessage("Time zone must be a valid IANA identifier.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZone));
    }

    private static bool BeValidIanaTimeZone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz)) return true;
        try { TimeZoneInfo.FindSystemTimeZoneById(tz); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}

public sealed class UpdateRestaurantRequestValidator : AbstractValidator<UpdateRestaurantRequest>
{
    public UpdateRestaurantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9\s\-()]{7,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).SetValidator(new AddressInputValidator()).When(x => x.Address is not null);
    }
}

public sealed class UpdateRestaurantStatusRequestValidator : AbstractValidator<UpdateRestaurantStatusRequest>
{
    private static readonly string[] Allowed = ["Draft", "Active", "TemporarilyClosed", "Closed"];

    public UpdateRestaurantStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => Allowed.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", Allowed)}.");
    }
}
