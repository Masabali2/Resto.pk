using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Context-aware image upload. Validates the file, stores bytes through the
/// storage abstraction, persists metadata and the destination-specific
/// association, and cleans up stored bytes if persistence fails.
/// </summary>
public sealed class AssetUploadService : IAssetUploadService
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IFileStorageService _storage;
    private readonly IImageDimensionReader _dimensions;
    private readonly ImageUploadOptions _options;
    private readonly IEnumerable<IUploadDestinationHandler> _handlers;
    private readonly IUploadDestinationContext _uploadDestinationContext;

    public AssetUploadService(
        ApplicationDbContext db,
        ITenantContext tenantContext,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IFileStorageService storage,
        IImageDimensionReader dimensions,
        IOptions<ImageUploadOptions> options,
        IEnumerable<IUploadDestinationHandler> handlers,
        IUploadDestinationContext uploadDestinationContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _storage = storage;
        _dimensions = dimensions;
        _options = options.Value;
        _handlers = handlers;
        _uploadDestinationContext = uploadDestinationContext;
    }

    public async Task<AssetUploadResult> UploadAsync(
        UploadDestination destination,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (destination == null)
            throw new DomainException("UPLOAD_DESTINATION_REQUIRED", "An upload destination is required.");

        if (file == null || file.Length == 0)
            throw new DomainException("UPLOAD_FILE_REQUIRED", "An image file is required.");

        if (file.Length > _options.MaxFileSizeBytes)
            throw new DomainException("UPLOAD_FILE_TOO_LARGE",
                $"File size exceeds the {_options.MaxFileSizeBytes / 1024 / 1024} MB limit.");

        var tenantId = _tenantContext.GetRequiredTenantId();
        var userId = _currentUser.UserId;
        if (!userId.HasValue || !_currentUser.IsAuthenticated)
            throw new DomainException("UPLOAD_AUTHENTICATION_REQUIRED", "You must be signed in to upload.");

        // Content signature (magic bytes) is authoritative; the client-supplied
        // MIME type is never trusted for storage or validation.
        var detectedContentType = _dimensions.DetectContentType(file.OpenReadStream());
        if (detectedContentType is null)
            throw new DomainException("UPLOAD_UNSUPPORTED_TYPE", "The file is not a supported image.");

        if (!_options.AllowedContentTypes.Contains(detectedContentType))
            throw new DomainException("UPLOAD_UNSUPPORTED_TYPE",
                $"'{detectedContentType}' is not a supported image type.");

        if (!string.IsNullOrEmpty(file.ContentType) &&
            !ContentTypesMatch(file.ContentType, detectedContentType))
            throw new DomainException("UPLOAD_MIME_MISMATCH",
                "The file content does not match the supplied content type.");

        var dimensions = _dimensions.ReadDimensions(file.OpenReadStream());
        if (dimensions is not null)
        {
            if (dimensions.Value.Width > _options.MaxWidth ||
                dimensions.Value.Height > _options.MaxHeight)
                throw new DomainException("UPLOAD_IMAGE_TOO_LARGE",
                    $"Image dimensions exceed {_options.MaxWidth}x{_options.MaxHeight}.");
        }

        var fileName = NormalizeFileName(file.FileName);

        // Persist bytes first. If the database commit below fails, the stored
        // bytes are removed in the compensating cleanup.
        var storageKey = await _storage.SaveAsync(
            tenantId, file.OpenReadStream(), fileName, detectedContentType, cancellationToken);

        var metadata = FileMetadata.Create(
            tenantId,
            fileName,
            storageKey,
            detectedContentType,
            file.Length,
            _options.StorageProviderName,
            dimensions?.Width,
            dimensions?.Height);

        _db.Files.Add(metadata);

        var handler = ResolveHandler(destination.Kind);
        var uploadContext = _uploadDestinationContext;

        try
        {
            await handler.ApplyAsync(metadata, destination, userId.Value, uploadContext, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensating cleanup: remove stored bytes when metadata/association
            // could not be persisted.
            try { await _storage.DeleteAsync(storageKey, cancellationToken); }
            catch { /* best-effort; the orphan is reported for lifecycle cleanup. */ }
            throw;
        }

        return new AssetUploadResult(
            metadata.Id,
            metadata.FileName,
            metadata.ContentType,
            metadata.FileSize,
            metadata.Width,
            metadata.Height,
            $"/api/v1/files/{Uri.EscapeDataString(storageKey)}",
            destination.Kind);
    }

    public async Task<bool> DeleteAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.GetRequiredTenantId();
        var userId = _currentUser.UserId;
        if (!userId.HasValue || !_currentUser.IsAuthenticated)
            throw new DomainException("UPLOAD_AUTHENTICATION_REQUIRED", "You must be signed in.");

        var isMember = await _db.StaffMemberships.AnyAsync(
            m => m.UserId == userId.Value && m.TenantId == tenantId && m.Status == MembershipStatus.Active,
            cancellationToken);
        if (!isMember)
            throw new DomainException("UPLOAD_FORBIDDEN", "You are not authorized to manage this tenant's assets.");

        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.Id == assetId && f.TenantId == tenantId, cancellationToken);
        if (file is null || file.IsDeleted)
            return false;

        // Never delete an asset that is still referenced by another resource.
        bool referenced = await _db.RestaurantAssets
                .AnyAsync(a => a.FileId == assetId && a.IsActive, cancellationToken)
            || await _db.MenuItems
                .AnyAsync(i => i.ImageFileId == assetId, cancellationToken);
        if (referenced)
            return false;

        file.MarkDeleted(_dateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        // Storage bytes are left for the delayed orphan-cleanup lifecycle so a
        // recently-unreferenced asset is not destroyed before references settle.
        return true;
    }

    private static string NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "image";

        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            return "image";

        return name;
    }

    private static bool ContentTypesMatch(string clientType, string detectedType)
    {
        var normalized = clientType.Trim().ToLowerInvariant();
        if (normalized == "image/jpg") normalized = "image/jpeg";
        return string.Equals(normalized, detectedType, StringComparison.OrdinalIgnoreCase);
    }

    private IUploadDestinationHandler ResolveHandler(string kind)
    {
        var handler = _handlers.FirstOrDefault(h =>
            string.Equals(h.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (handler is null)
            throw new DomainException("UPLOAD_INVALID_DESTINATION",
                $"The upload destination '{kind}' is not supported.");
        return handler;
    }
}
