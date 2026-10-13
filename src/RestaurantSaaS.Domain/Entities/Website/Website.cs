
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

public class Website : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }
    public Guid WebsiteTemplateId { get; private set; }
    public Guid? PublishedConfigurationId { get; private set; }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;

    public WebsiteStatus Status { get; private set; } = WebsiteStatus.Draft;
    public bool IsPublished { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Restaurant Restaurant { get; private set; } = null!;
    public WebsiteTemplate Template { get; private set; } = null!;

    public ICollection<WebsiteConfiguration> Configurations { get; private set; }
        = new List<WebsiteConfiguration>();

    private Website()
    {
    }

    public static Website Create(
        Guid tenantId,
        Guid restaurantId,
        Guid websiteTemplateId,
        string name,
        string slug)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("WEBSITE_TENANT_REQUIRED", "A website must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException("WEBSITE_RESTAURANT_REQUIRED", "A website must belong to a restaurant.");

        if (websiteTemplateId == Guid.Empty)
            throw new DomainException("WEBSITE_TEMPLATE_REQUIRED", "A website requires a template.");

        ValidateName(name);

        return new Website
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            WebsiteTemplateId = websiteTemplateId,
            Name = name.Trim(),
            Slug = NormalizeSlug(slug),
            Status = WebsiteStatus.Draft,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string? name, string? slug, Guid? templateId)
    {
        if (name is not null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (slug is not null)
            Slug = NormalizeSlug(slug);

        if (templateId.HasValue)
        {
            if (templateId.Value == Guid.Empty)
                throw new DomainException("WEBSITE_TEMPLATE_INVALID", "Template ID cannot be empty.");

            WebsiteTemplateId = templateId.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish(
        WebsiteConfiguration draftConfiguration,
        WebsiteTemplate template,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(draftConfiguration);
        ArgumentNullException.ThrowIfNull(template);

        if (draftConfiguration.WebsiteId != Id)
            throw new DomainException(
                "WEBSITE_CONFIGURATION_MISMATCH",
                "The configuration must belong to this website.");

        if (!draftConfiguration.IsDraft)
            throw new DomainException(
                "WEBSITE_PUBLISH_NO_DRAFT",
                "Publishing requires a draft configuration version.");

        if (template.Id != WebsiteTemplateId)
            throw new DomainException(
                "WEBSITE_TEMPLATE_MISMATCH",
                "The supplied template does not belong to this website.");

        if (!template.IsActive)
            throw new DomainException(
                "WEBSITE_TEMPLATE_INACTIVE",
                "An inactive template cannot be published.");

        draftConfiguration.Publish(utcNow);

        PublishedConfigurationId = draftConfiguration.Id;
        Status = WebsiteStatus.Published;
        IsPublished = true;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Unpublish(DateTime utcNow)
    {
        if (!IsPublished)
            throw new DomainException(
                "WEBSITE_NOT_PUBLISHED",
                "Website is not currently published.");

        Status = WebsiteStatus.Unpublished;
        IsPublished = false;
        PublishedConfigurationId = null;
        UpdatedAt = utcNow;
    }

    public static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("WEBSITE_SLUG_REQUIRED", "Website slug is required.");

        var normalized = new string(
            slug.Trim()
                .ToLowerInvariant()
                .Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-')
                .ToArray());

        normalized = string.Join(
            '-',
            normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length is < 2 or > 100)
            throw new DomainException(
                "WEBSITE_SLUG_INVALID",
                "Website slug must be 2-100 characters and contain valid URL characters.");

        return normalized;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("WEBSITE_NAME_REQUIRED", "Website name is required.");

        if (name.Trim().Length > 150)
            throw new DomainException(
                "WEBSITE_NAME_TOO_LONG",
                "Website name cannot exceed 150 characters.");
    }
}
