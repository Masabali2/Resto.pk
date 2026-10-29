using FluentValidation;
using RestaurantSaaS.Application.Common.Files;
using System.Text.Json;

namespace RestaurantSaaS.Application.Common.Files;

public sealed class UploadImageRequestValidator : AbstractValidator<UploadImageRequest>
{
    private static readonly JsonSerializerOptions DestinationOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UploadImageRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("An image file is required.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("An upload destination is required.")
            .Custom((destination, context) =>
            {
                if (string.IsNullOrWhiteSpace(destination))
                    return;

                UploadDestination? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<UploadDestination>(
                        destination!, DestinationOptions);
                }
                catch (JsonException)
                {
                    context.AddFailure("Destination is not valid JSON.");
                    return;
                }

                if (parsed is null)
                {
                    context.AddFailure("Destination could not be parsed.");
                    return;
                }

                switch (parsed)
                {
                    case WebsiteSectionDestination ws:
                        if (ws.WebsiteId == Guid.Empty)
                            context.AddFailure("Website id is required for a website-section upload.");
                        if (string.IsNullOrWhiteSpace(ws.SectionType) ||
                            !new[] { "hero", "about", "gallery" }.Contains(ws.SectionType,
                                StringComparer.OrdinalIgnoreCase))
                            context.AddFailure("Section type must be hero, about or gallery.");
                        break;

                    case RestaurantLogoDestination rl:
                        if (rl.RestaurantId == Guid.Empty)
                            context.AddFailure("Restaurant id is required for a restaurant-logo upload.");
                        break;

                    case MenuItemDestination mi:
                        if (mi.MenuItemId == Guid.Empty)
                            context.AddFailure("Menu item id is required for a menu-item upload.");
                        break;

                    default:
                        context.AddFailure("The upload destination kind is not supported.");
                        break;
                }
            });
    }
}
