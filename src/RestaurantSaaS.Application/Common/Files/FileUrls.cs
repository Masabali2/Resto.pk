namespace RestaurantSaaS.Application.Common.Files;

/// <summary>
/// Canonical public delivery URLs for stored files.
/// </summary>
public static class FileUrls
{
    /// <summary>
    /// Builds the delivery URL for an immutable file metadata id.
    /// The storage key remains internal to the storage layer and is never
    /// exposed in a public URL. No expiration/query string is appended: the
    /// endpoint authenticates the caller and validates tenant membership.
    /// </summary>
    public static string ForFile(Guid fileId) => $"/api/v1/files/{fileId:D}";
}
