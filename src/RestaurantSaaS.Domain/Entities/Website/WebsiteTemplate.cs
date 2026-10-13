
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

public class WebsiteTemplate : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public Guid? PreviewImageFileId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private WebsiteTemplate()
    {
    }

    public static WebsiteTemplate Create(
        string name,
        string templateKey,
        string? description = null)
    {
        ValidateName(name);

        if (string.IsNullOrWhiteSpace(templateKey))
            throw new DomainException(
                "TEMPLATE_KEY_REQUIRED",
                "Template key is required.");

        var normalizedKey = templateKey.Trim().ToLowerInvariant();

        if (normalizedKey.Length > 100 ||
            !normalizedKey.All(c =>
                char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_'))
        {
            throw new DomainException(
                "TEMPLATE_KEY_INVALID",
                "Template key must contain only ASCII letters, digits, hyphens, dots, or underscores, up to 100 characters.");
        }

        return new WebsiteTemplate
        {
            Name = name.Trim(),
            TemplateKey = normalizedKey,
            Description = NormalizeDescription(description),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Platform-admin assignment of the preview image.
    /// The caller is responsible for validating the file.
    /// </summary>
    public void AssignPreviewImage(Guid? fileId)
    {
        SetPreviewImage(fileId);
    }

    public void Update(string? name, string? description, bool? isActive)
    {
        if (name is not null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (description is not null)
            Description = NormalizeDescription(description);

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPreviewImage(Guid? fileId)
    {
        if (fileId == Guid.Empty)
            throw new DomainException(
                "TEMPLATE_PREVIEW_INVALID",
                "Preview image ID cannot be empty.");

        if (PreviewImageFileId == fileId)
            return;

        PreviewImageFileId = fileId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive)
            return;

        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(
                "TEMPLATE_NAME_REQUIRED",
                "Template name is required.");

        if (name.Trim().Length > 150)
            throw new DomainException(
                "TEMPLATE_NAME_TOO_LONG",
                "Template name cannot exceed 150 characters.");
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }
}

