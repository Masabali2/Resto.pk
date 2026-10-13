using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

/// <summary>
/// Platform-defined website template catalog. NOT tenant-owned (shared), so no
/// TenantId (requirement #38). TemplateKey is the stable identifier the
/// front-end renderer uses to select a component set — UNIQUE at DB level.
/// </summary>
public class WebsiteTemplate : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Stable machine key, e.g. "classic-dine-v1". Immutable after creation.</summary>
    public string TemplateKey { get; private set; } = null!;

    /// <summary>Preview image (FileMetadata row).</summary>
    public Guid? PreviewImageFileId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static WebsiteTemplate Create(string name, string templateKey, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("TEMPLATE_NAME_REQUIRED", "Template name is required.");

        if (string.IsNullOrWhiteSpace(templateKey))
            throw new DomainException("TEMPLATE_KEY_REQUIRED", "Template key is required.");

        // Template keys are code identifiers: kebab/lowercase alnum + dots/dashes.
        if (templateKey.Length > 100 || !templateKey.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '_'))
            throw new DomainException("TEMPLATE_KEY_INVALID",
                "Template key must be 100 chars of letters, digits, '-' , '.' or '_'.");

        return new WebsiteTemplate
        {
            Name = name.Trim(),
            TemplateKey = templateKey.ToLowerInvariant(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Platform-admin assignment of the preview image (validated against Files by the caller).</summary>
    public void AssignPreviewImage(Guid? fileId)
    {
        PreviewImageFileId = fileId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? name, string? description, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        if (description is not null)
            Description = description.Trim();

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }
}
