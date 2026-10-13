namespace RestaurantSaaS.Application.Common.Files;

/// <summary>
/// Catalog image rules used when assigning an existing FileMetadata row to a menu item.
/// File upload itself is not part of Phase 4 — only authorized references are accepted.
/// </summary>
public static class MenuImageRules
{
    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
}
