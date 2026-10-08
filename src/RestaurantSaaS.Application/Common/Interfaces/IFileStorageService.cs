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

    /// <summary>Time-limited access URL; never a raw filesystem path.</summary>
    string GetPresignedUrl(string storageKey, TimeSpan expiration);
}
