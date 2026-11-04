namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Storage abstraction (local disk in dev; Azure Blob/S3 later) — domain/app
/// logic never changes. Keys are ALWAYS tenant-partitioned: tenant/{tenantId}/…
/// Callers pass relative keys; the implementation prefixes the tenant segment
/// so unrestricted paths can't be constructed by clients.
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveAsync(
        Guid? tenantId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> ReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy storage-key delivery URL. The local implementation does not sign
    /// or validate the expiration, so it is not used for new asset URLs.
    /// New code must use <see cref="RestaurantSaaS.Application.Common.Files.FileUrls.ForFile"/> with the file id.
    /// </summary>
    string GetPresignedUrl(string storageKey, TimeSpan expiration);
}
