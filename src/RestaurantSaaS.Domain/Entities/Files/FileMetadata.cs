using RestaurantSaaS.Domain.Common;

namespace RestaurantSaaS.Domain.Entities.Files;

/// <summary>
/// SQL-side metadata for an uploaded file. The BINARY lives in object storage
/// (local disk in dev; Blob/S3/Cloudinary later) addressed by StorageKey —
/// never store bytes in SQL Server (requirement #5/#22).
///
/// TenantId is nullable because platform-owned assets (template previews,
/// seeded images) exist before/outside a tenant. File access must be validated
/// against the requesting tenant; storage keys are partitioned
/// "tenant/{tenantId}/..." by IFileStorageService.
/// </summary>
public class FileMetadata : BaseEntity
{
    public Guid? TenantId { get; private set; }

    /// <summary>Original client file name — display only, never used as a path.</summary>
    public string FileName { get; private set; } = null!;

    /// <summary>Opaque storage location (GUID-based). Unique at DB level.</summary>
    public string StorageKey { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }

    /// <summary>"local" | "azure-blob" | "s3" …</summary>
    public string StorageProvider { get; private set; } = "local";

    public DateTime CreatedAt { get; private set; }

    /// <summary>Set when the object was deleted from storage (tombstone).</summary>
    public DateTime? DeletedAt { get; private set; }

    public static FileMetadata Create(
        Guid? tenantId,
        string fileName,
        string storageKey,
        string contentType,
        long fileSize,
        string storageProvider)
    {
        return new FileMetadata
        {
            TenantId = tenantId,
            FileName = fileName,
            StorageKey = storageKey,
            ContentType = contentType,
            FileSize = fileSize,
            StorageProvider = storageProvider,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkDeleted(DateTime utcNow) => DeletedAt = utcNow;
}
