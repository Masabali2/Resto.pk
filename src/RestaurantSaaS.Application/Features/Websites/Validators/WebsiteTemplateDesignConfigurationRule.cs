using FluentValidation;
using RestaurantSaaS.Application.Common;

namespace RestaurantSaaS.Application.Features.Websites.Validators;

/// <summary>
/// Reusable FluentValidation rules for the design configuration of a website
/// template.
///
/// The design configuration is validated by the single allowlist schema
/// validator of the Application layer
/// (<see cref="WebsiteTemplateDesignSchema"/>), which is shared with the
/// domain entity. The rules below only decide WHETHER a configuration has to
/// be supplied — they never re-implement the schema, so the two layers can
/// never drift apart.
///
/// Every rule validates the document exactly ONCE. The rejection reason
/// returned by the schema validator is used as the failure message, so the
/// expensive parse is never repeated for the same input.
/// </summary>
internal static class WebsiteTemplateDesignConfigurationRule
{
    /// <summary>
    /// Rule for creation: the template has to be shipped with a default design
    /// configuration, otherwise it could never be rendered by the generic
    /// template renderer.
    /// </summary>
    public static void ApplyRequired<TProperty>(
        string? designConfigurationJson,
        ValidationContext<TProperty> context)
    {
        if (string.IsNullOrWhiteSpace(designConfigurationJson))
        {
            context.AddFailure(
                "A default design configuration is required to create a template.");
            return;
        }

        AddSchemaFailure(designConfigurationJson, context);
    }

    /// <summary>
    /// Rule for updates: a configuration that is supplied has to be valid. A
    /// configuration that is omitted (<c>null</c>) leaves the stored one
    /// untouched, so a metadata-only update never resets the design.
    /// </summary>
    public static void ApplyOptional<TProperty>(
        string? designConfigurationJson,
        ValidationContext<TProperty> context)
    {
        if (designConfigurationJson is null)
            return;

        AddSchemaFailure(designConfigurationJson, context);
    }

    private static void AddSchemaFailure<TProperty>(
        string designConfigurationJson,
        ValidationContext<TProperty> context)
    {
        var error = WebsiteTemplateDesignSchema.Validate(designConfigurationJson);

        if (error is not null)
            context.AddFailure(error);
    }
}
