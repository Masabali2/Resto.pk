
using System.Text.Json;
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

public class WebsiteTemplate : AuditableEntity
{
    /// <summary>
    /// Upper bound for the persisted design configuration document. The design
    /// configuration is a small, structured document (palette, typography,
    /// layout, sections and sample preview copy) — 100 KB is already far
    /// beyond any legitimate template. The limit is enforced by the domain
    /// and by request validation; the column itself is stored as
    /// <c>nvarchar(max)</c>.
    /// </summary>
    public const int MaxDesignConfigurationLength = 100_000;

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public Guid? PreviewImageFileId { get; private set; }

    /// <summary>
    /// Versioned, validated design configuration describing how the template
    /// looks (palette, typography, layout variants, section order and safe
    /// sample preview content). It is DATA, never code: no HTML, JavaScript,
    /// CSS or expressions are stored here.
    /// </summary>
    public string? DesignConfigurationJson { get; private set; }

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

    /// <summary>
    /// Stores the design configuration document.
    /// The value has to be a non-empty, well-formed JSON document that is not
    /// longer than <see cref="MaxDesignConfigurationLength"/> characters.
    /// The deeper (semantic) shape rules live in the Application layer schema
    /// validator, so the domain stays free of any transport concern.
    /// </summary>
    /// <exception cref="DomainException">
    /// The configuration is missing, oversized or not well-formed JSON.
    /// </exception>
    public void SetDesignConfiguration(string designConfigurationJson)
    {
        ValidateDesignConfiguration(designConfigurationJson);

        if (DesignConfigurationJson == designConfigurationJson)
            return;

        DesignConfigurationJson = designConfigurationJson;
        UpdatedAt = DateTime.UtcNow;
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

    private static void ValidateDesignConfiguration(string? designConfigurationJson)
    {
        if (designConfigurationJson is null || string.IsNullOrWhiteSpace(designConfigurationJson))
            throw new DomainException(
                "TEMPLATE_DESIGN_CONFIGURATION_REQUIRED",
                "Design configuration JSON is required.");

        if (designConfigurationJson.Length > MaxDesignConfigurationLength)
            throw new DomainException(
                "TEMPLATE_DESIGN_CONFIGURATION_TOO_LARGE",
                $"Design configuration JSON exceeds the {MaxDesignConfigurationLength} character limit.");

        try
        {
            using var document = JsonDocument.Parse(designConfigurationJson);
        }
        catch (JsonException ex)
        {
            throw new DomainException(
                "TEMPLATE_DESIGN_CONFIGURATION_INVALID",
                "Design configuration must be valid JSON.",
                ex);
        }
    }
}

