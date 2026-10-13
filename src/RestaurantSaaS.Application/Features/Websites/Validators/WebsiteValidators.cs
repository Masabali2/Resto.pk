using FluentValidation;
using RestaurantSaaS.Application.Common;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Application.Features.Websites.Validators;

public sealed class CreateWebsiteTemplateRequestValidator : AbstractValidator<CreateWebsiteTemplateRequest>
{
    public CreateWebsiteTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Template key may contain only letters, digits, '-', '.' or '_'.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateWebsiteTemplateRequestValidator : AbstractValidator<UpdateWebsiteTemplateRequest>
{
    public UpdateWebsiteTemplateRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x).Must(x => x.Name is not null || x.Description is not null || x.IsActive is not null)
            .WithMessage("At least one field must be provided.");
    }
}

public sealed class CreateWebsiteRequestValidator : AbstractValidator<CreateWebsiteRequest>
{
    public CreateWebsiteRequestValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.WebsiteTemplateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9][a-z0-9-]*[a-z0-9]$").WithMessage("Slug must be lowercase alphanumeric with single hyphens.");
    }
}

public sealed class UpdateWebsiteRequestValidator : AbstractValidator<UpdateWebsiteRequest>
{
    public UpdateWebsiteRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(150);
        RuleFor(x => x.Slug).MaximumLength(100)
            .Matches("^[a-z0-9][a-z0-9-]*[a-z0-9]$").When(x => !string.IsNullOrEmpty(x.Slug))
            .WithMessage("Slug must be lowercase alphanumeric with single hyphens.");
    }
}

public sealed class SaveWebsiteDraftRequestValidator : AbstractValidator<SaveWebsiteDraftRequest>
{
    public SaveWebsiteDraftRequestValidator()
    {
        RuleFor(x => x.ConfigurationJson)
            .NotEmpty()
            .MaximumLength(Domain.Entities.Websites.WebsiteConfiguration.MaxJsonLength)
            .Must(json => WebsiteConfigSchema.Validate(json) is null)
            .WithMessage((_, json) => WebsiteConfigSchema.Validate(json ?? "") ?? "Invalid configuration.");
    }
}
