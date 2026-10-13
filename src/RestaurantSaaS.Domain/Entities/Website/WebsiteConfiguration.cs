
using System.Text.Json;
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Websites;

public class WebsiteConfiguration : AuditableEntity
{
    public const int MaxJsonLength = 1_048_576;

    public Guid WebsiteId { get; private set; }
    public int ConfigurationVersion { get; private set; }
    public bool IsDraft { get; private set; } = true;
    public string ConfigurationJson { get; private set; } = null!;
    public DateTime? PublishedAt { get; private set; }

    public Website Website { get; private set; } = null!;

    private WebsiteConfiguration()
    {
    }

    public static WebsiteConfiguration CreateDraft(
        Guid websiteId,
        string configurationJson,
        int nextVersion)
    {
        if (websiteId == Guid.Empty)
            throw new DomainException(
                "CONFIG_WEBSITE_REQUIRED",
                "Configuration requires a website.");

        if (nextVersion < 1)
            throw new DomainException(
                "CONFIG_VERSION_INVALID",
                "Configuration version must be at least 1.");

        ValidateJson(configurationJson);

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
            throw new DomainException(
                "CONFIG_NOT_DRAFT",
                "Only draft configurations can be edited. Create a new draft version.");

        ValidateJson(configurationJson);

        if (ConfigurationJson == configurationJson)
            return;

        ConfigurationJson = configurationJson;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void Publish(DateTime utcNow)
    {
        if (!IsDraft)
            throw new DomainException(
                "CONFIG_ALREADY_PUBLISHED",
                "Configuration version is already published.");

        IsDraft = false;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;
    }

    private static void ValidateJson(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            throw new DomainException(
                "CONFIG_JSON_REQUIRED",
                "Configuration JSON is required.");

        if (configurationJson.Length > MaxJsonLength)
            throw new DomainException(
                "CONFIG_JSON_TOO_LARGE",
                "Configuration JSON exceeds the 1 MB limit.");

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
        }
        catch (JsonException ex)
        {
            throw new DomainException(
                "CONFIG_JSON_INVALID",
                "Configuration must be valid JSON.",
                ex);
        }
    }
}

