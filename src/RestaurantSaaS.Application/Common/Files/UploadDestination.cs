using System.Text.Json.Serialization;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Common.Files;

/// <summary>
/// Validated, server-side upload destination. Clients supply a destination
/// descriptor; the service resolves tenant and ownership from trusted context
/// and dispatches to the matching destination-specific assignment handler.
/// This avoids trusting raw role strings, arbitrary entity IDs, or property
/// paths from the caller.
/// </summary>
[JsonDerivedType(typeof(WebsiteSectionDestination), typeDiscriminator: "website-section")]
[JsonDerivedType(typeof(RestaurantLogoDestination), typeDiscriminator: "restaurant-logo")]
[JsonDerivedType(typeof(MenuItemDestination), typeDiscriminator: "menu-item")]
public abstract record UploadDestination
{
    public string Kind { get; init; } = string.Empty;
}

/// <summary>Associate the uploaded file with a website draft section.</summary>
public sealed record WebsiteSectionDestination : UploadDestination
{
    public WebsiteSectionDestination() => Kind = "website-section";

    public Guid WebsiteId { get; init; }
    public string SectionType { get; init; } = string.Empty; // hero | about | gallery
    public string SettingKey { get; init; } = string.Empty;   // e.g. imageUrl | galleryImageIds
    public int? DisplayOrder { get; init; }
}

/// <summary>Associate the uploaded file as a restaurant branding asset.</summary>
public sealed record RestaurantLogoDestination : UploadDestination
{
    public RestaurantLogoDestination() => Kind = "restaurant-logo";

    public Guid RestaurantId { get; init; }
}

/// <summary>Associate the uploaded file with a menu item.</summary>
public sealed record MenuItemDestination : UploadDestination
{
    public MenuItemDestination() => Kind = "menu-item";

    public Guid MenuItemId { get; init; }
}

/// <summary>Outcome of a successful image upload and association.</summary>
public sealed record AssetUploadResult(
    Guid AssetId,
    string FileName,
    string ContentType,
    long FileSize,
    int? Width,
    int? Height,
    string? PreviewUrl,
    string DestinationKind);

/// <summary>Configurable image upload limits. Bound from configuration.</summary>
public sealed class ImageUploadOptions
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;
    public const int DefaultMaxDimension = 4096;

    public IReadOnlySet<string> AllowedContentTypes { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp"
        };

    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;
    public int MaxWidth { get; init; } = DefaultMaxDimension;
    public int MaxHeight { get; init; } = DefaultMaxDimension;
    public bool RejectSvg { get; init; } = true;
    public string StorageProviderName { get; init; } = "local";
}
