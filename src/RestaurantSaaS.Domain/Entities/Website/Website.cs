using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

/// <summary>
/// The public website of one restaurant. Slug uniqueness is TENANT-AWARE:
/// UNIQUE(TenantId, Slug) — two tenants may both use "menu", but one tenant
/// cannot have duplicate slugs. Public resolution (subdomain/path -> tenant ->
/// website) relies on this index.
///
/// Composite FK (TenantId, RestaurantId) -> Restaurants(TenantId, Id) prevents
/// cross-tenant website->restaurant references at the database level.
/// </summary>
public class Website : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }
    public Guid WebsiteTemplateId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>URL segment under the tenant scope. Unique per tenant.</summary>
    public string Slug { get; private set; } = null!;

    public WebsiteStatus Status { get; private set; } = WebsiteStatus.Draft;
    public bool IsPublished { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Restaurant Restaurant { get; private set; } = null!;
    public WebsiteTemplate Template { get; private set; } = null!;
    public ICollection<WebsiteConfiguration> Configurations { get; set; } = new List<WebsiteConfiguration>();

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

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("WEBSITE_NAME_REQUIRED", "Website name is required.");

        var normalizedSlug = NormalizeSlug(slug);

        return new Website
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            WebsiteTemplateId = websiteTemplateId,
            Name = name.Trim(),
            Slug = normalizedSlug,
            Status = WebsiteStatus.Draft,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string? name, string? slug, Guid? templateId)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        if (!string.IsNullOrWhiteSpace(slug))
            Slug = NormalizeSlug(slug);

        if (templateId is { } tid && tid != Guid.Empty)
            WebsiteTemplateId = tid;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Publishes the website with the given draft configuration snapshot.
    /// Rule: publishing requires an existing draft configuration.
    /// </summary>
    public void Publish(WebsiteConfiguration draftConfiguration, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(draftConfiguration);

        if (!draftConfiguration.IsDraft)
            throw new DomainException("WEBSITE_PUBLISH_NO_DRAFT",
                "Publishing requires a draft configuration version.");

        draftConfiguration.Publish(utcNow);
        Status = WebsiteStatus.Published;
        IsPublished = true;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Unpublish(DateTime utcNow)
    {
        if (!IsPublished)
            throw new DomainException("WEBSITE_NOT_PUBLISHED", "Website is not published.");

        Status = WebsiteStatus.Unpublished;
        IsPublished = false;
        UpdatedAt = utcNow;
    }

    public static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("WEBSITE_SLUG_REQUIRED", "Website slug is required.");

        var normalized = new string(slug.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray())
            .Trim('-');

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length is < 2 or > 100)
            throw new DomainException("WEBSITE_SLUG_INVALID", "Website slug must be 2-100 characters.");

        return normalized;
    }
}
