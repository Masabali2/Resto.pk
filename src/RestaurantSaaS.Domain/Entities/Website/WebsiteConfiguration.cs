using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

/// <summary>
/// Versioned JSON configuration document for a website builder
/// (sections, colors, blocks…). This is the ONE place JSON persistence is
/// allowed: it is genuinely schema-flexible presentation data, not relational
/// business data (requirement #5 / WebsiteConfiguration guidance).
///
/// Draft/publish model: editing always mutates a draft version; publishing
/// freezes it. Previous versions are retained (history) — no TenantId needed,
/// tenancy derives through Website.
/// </summary>
public class WebsiteConfiguration : AuditableEntity
{
    public Guid WebsiteId { get; private set; }

    /// <summary>Monotonic version number per website.</summary>
    public int ConfigurationVersion { get; private set; }

    public bool IsDraft { get; private set; } = true;

    /// <summary>Builder document. Validated (size/JSON well-formedness) before save.</summary>
    public string ConfigurationJson { get; private set; } = null!;

    public DateTime? PublishedAt { get; private set; }

    public Website Website { get; private set; } = null!;

    /// <summary>Upper bound to keep documents out of LOB abuse; builder UIs stay far below.</summary>
    public const int MaxJsonLength = 1_048_576; // 1 MB

    public static WebsiteConfiguration CreateDraft(Guid websiteId, string configurationJson, int nextVersion)
    {
        if (websiteId == Guid.Empty)
            throw new DomainException("CONFIG_WEBSITE_REQUIRED", "Configuration requires a website.");

        ValidateJson(configurationJson);

        if (nextVersion < 1)
            throw new DomainException("CONFIG_VERSION_INVALID", "Configuration version must be >= 1.");

        return new WebsiteConfiguration
        {
            WebsiteId = websiteId,
            ConfigurationVersion = nextVersion,
            ConfigurationJson = configurationJson,
            IsDraft = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateDraft(string configurationJson)
    {
        if (!IsDraft)
            throw new DomainException("CONFIG_NOT_DRAFT", "Only draft configurations can be edited; create a new draft version.");

        ValidateJson(configurationJson);
        ConfigurationJson = configurationJson;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Called only by Website.Publish — freezes the draft.</summary>
    public void Publish(DateTime utcNow)
    {
        if (!IsDraft)
            throw new DomainException("CONFIG_ALREADY_PUBLISHED", "Configuration version is already published.");

        IsDraft = false;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private static void ValidateJson(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            throw new DomainException("CONFIG_JSON_REQUIRED", "Configuration JSON is required.");

        if (configurationJson.Length > MaxJsonLength)
            throw new DomainException("CONFIG_JSON_TOO_LARGE", "Configuration JSON exceeds the 1 MB limit.");

        // Well-formedness check only — semantic validation belongs to the builder DTO layer.
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(configurationJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new DomainException("CONFIG_JSON_INVALID", "Configuration must be valid JSON.", ex);
        }
    }
}
