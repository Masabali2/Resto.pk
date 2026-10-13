
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Files;

public class FileMetadata : BaseEntity
{
    public const int MaxFileNameLength = 255;
    public const int MaxStorageKeyLength = 500;
    public const int MaxContentTypeLength = 150;
    public const int MaxStorageProviderLength = 50;
    public const long MaxFileSizeBytes = 104857600;

    public Guid? TenantId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string StorageKey { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long FileSize { get; private set; }

    public string StorageProvider { get; private set; } = "local";

    public DateTime CreatedAt { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    private FileMetadata()
    {
    }

    public static FileMetadata Create(
        Guid? tenantId,
        string fileName,
        string storageKey,
        string contentType,
        long fileSize,
        string storageProvider)
    {
        ValidateFileName(fileName);
        ValidateStorageKey(storageKey);
        ValidateContentType(contentType);
        ValidateFileSize(fileSize);
        ValidateStorageProvider(storageProvider);

        if (tenantId == Guid.Empty)
            throw new DomainException(
                "FILE_TENANT_INVALID",
                "Tenant ID cannot be an empty GUID.");

        return new FileMetadata
        {
            TenantId = tenantId,
            FileName = fileName.Trim(),
            StorageKey = storageKey.Trim(),
            ContentType = contentType.Trim().ToLowerInvariant(),
            FileSize = fileSize,
            StorageProvider = storageProvider.Trim().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkDeleted(DateTime utcNow)
    {
        if (DeletedAt.HasValue)
            return;

        if (utcNow.Kind != DateTimeKind.Utc)
            throw new DomainException(
                "FILE_DELETION_TIME_INVALID",
                "Deletion time must be UTC.");

        DeletedAt = utcNow;
    }

    public bool IsDeleted => DeletedAt.HasValue;

    public bool IsAvailable => !IsDeleted;

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException(
                "FILE_NAME_REQUIRED",
                "File name is required.");

        if (fileName.Trim().Length > MaxFileNameLength)
            throw new DomainException(
                "FILE_NAME_TOO_LONG",
                $"File name cannot exceed {MaxFileNameLength} characters.");
    }

    private static void ValidateStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException(
                "FILE_STORAGE_KEY_REQUIRED",
                "Storage key is required.");

        if (storageKey.Trim().Length > MaxStorageKeyLength)
            throw new DomainException(
                "FILE_STORAGE_KEY_TOO_LONG",
                $"Storage key cannot exceed {MaxStorageKeyLength} characters.");

        if (storageKey.Contains('\\') || storageKey.StartsWith('/'))
            throw new DomainException(
                "FILE_STORAGE_KEY_INVALID",
                "Storage key format is invalid.");
    }

    private static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new DomainException(
                "FILE_CONTENT_TYPE_REQUIRED",
                "Content type is required.");

        if (contentType.Trim().Length > MaxContentTypeLength)
            throw new DomainException(
                "FILE_CONTENT_TYPE_TOO_LONG",
                $"Content type cannot exceed {MaxContentTypeLength} characters.");
    }

    private static void ValidateFileSize(long fileSize)
    {
        if (fileSize <= 0 || fileSize > MaxFileSizeBytes)
            throw new DomainException(
                "FILE_SIZE_INVALID",
                $"File size must be between 1 byte and {MaxFileSizeBytes} bytes.");
    }

    private static void ValidateStorageProvider(string storageProvider)
    {
        if (string.IsNullOrWhiteSpace(storageProvider))
            throw new DomainException(
                "FILE_STORAGE_PROVIDER_REQUIRED",
                "Storage provider is required.");

        if (storageProvider.Trim().Length > MaxStorageProviderLength)
            throw new DomainException(
                "FILE_STORAGE_PROVIDER_TOO_LONG",
                $"Storage provider cannot exceed {MaxStorageProviderLength} characters.");
    }
}

