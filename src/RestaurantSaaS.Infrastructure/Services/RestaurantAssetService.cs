using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class RestaurantAssetService(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService,
    IFileStorageService storage,
    TenantAccessGuard accessGuard) : IRestaurantAssetService
{
    private static readonly Dictionary<string, AssetType> AssetTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["logo"] = AssetType.Logo,
        ["favicon"] = AssetType.Favicon,
        ["coverimage"] = AssetType.CoverImage,
        ["banner"] = AssetType.Banner,
        ["mobilelogo"] = AssetType.MobileLogo
    };

    public async Task<IReadOnlyList<RestaurantAssetDto>> ListAsync(Guid restaurantId, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantView, ct);
        await EnsureRestaurantOwnershipAsync(restaurantId, ct);

        var rows = await db.RestaurantAssets.AsNoTracking()
            .Where(a => a.RestaurantId == restaurantId && a.IsActive)
            .OrderBy(a => a.AssetType)
            .Select(a => new
            {
                a.Id,
                a.AssetType,
                a.FileId,
                a.File.FileName,
                a.File.ContentType,
                a.File.FileSize,
                a.File.StorageKey,
                a.CreatedAt,
                a.UpdatedAt
            })
            .ToListAsync(ct);

        return rows.Select(a => new RestaurantAssetDto(
            a.Id,
            a.AssetType.ToString(),
            a.FileId,
            a.FileName,
            a.ContentType,
            a.FileSize,
            a.CreatedAt,
            a.UpdatedAt,
            storage.GetPresignedUrl(a.StorageKey, UrlLifetime))).ToList();
    }

    public async Task<RestaurantAssetDto> UploadAsync(
        Guid restaurantId,
        string assetType,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var tenantId = await GetActiveTenantIdAsync(Permissions.RestaurantUpdate, ct);
        await EnsureRestaurantOwnershipAsync(restaurantId, ct);

        if (!AssetTypeNames.TryGetValue(assetType ?? string.Empty, out var type) ||
            !RestaurantAssetRules.IsAllowed(type))
        {
            throw new DomainException(
                "ASSET_TYPE_INVALID",
                "The specified asset type is not supported for restaurant branding.");
        }

        ValidateImageContent(contentType);

        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);

        if (buffered.Length == 0)
            throw new DomainException("ASSET_FILE_EMPTY", "The uploaded file is empty.");

        if (buffered.Length > RestaurantAssetRules.MaxSizeFor(type))
            throw new DomainException(
                "ASSET_FILE_TOO_LARGE",
                $"This asset type cannot exceed {RestaurantAssetRules.MaxSizeFor(type)} bytes.");

        buffered.Position = 0;
        ValidateMagicBytes(buffered, contentType);

        var safeFileName = SanitizeFileName(fileName);
        buffered.Position = 0;

        var storageKey = await storage.SaveAsync(tenantId, buffered, safeFileName, contentType, ct);

        try
        {
            var file = FileMetadata.Create(
                tenantId,
                safeFileName,
                storageKey,
                contentType.ToLowerInvariant(),
                buffered.Length,
                "local");

            var existing = await db.RestaurantAssets
                .FirstOrDefaultAsync(a =>
                    a.RestaurantId == restaurantId && a.AssetType == type && a.IsActive, ct);

            string? replacedKey = null;

            if (existing is not null)
            {
                replacedKey = await GetStorageKeyAsync(existing.FileId, ct);

                existing.SetActive(false);
                existing.UpdatedAt = DateTime.UtcNow;
            }

            db.Files.Add(file);
            var replacement = RestaurantAsset.Create(restaurantId, file.Id, type);
            db.RestaurantAssets.Add(replacement);
            await db.SaveChangesAsync(ct);

            if (replacedKey is not null)
                await TryDeletePhysicalFileAsync(replacedKey);

            return ToDto(replacement, file);
        }
        catch
        {
            await TryDeletePhysicalFileAsync(storageKey);
            throw;
        }
    }

    public async Task DeleteAsync(Guid restaurantId, Guid assetId, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantUpdate, ct);
        await EnsureRestaurantOwnershipAsync(restaurantId, ct);

        var asset = await db.RestaurantAssets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.RestaurantId == restaurantId, ct)
            ?? throw new NotFoundException("RestaurantAsset", assetId);

        if (!asset.IsActive)
            throw new NotFoundException("RestaurantAsset", assetId);

        var storageKey = await GetStorageKeyAsync(asset.FileId, ct);

        asset.SetActive(false);
        asset.UpdatedAt = DateTime.UtcNow;

        var file = await db.Files.FirstOrDefaultAsync(f => f.Id == asset.FileId, ct);
        file?.MarkDeleted(DateTime.UtcNow);

        await db.SaveChangesAsync(ct);

        await TryDeletePhysicalFileAsync(storageKey);
    }

    public async Task<IReadOnlyList<PublicRestaurantAssetDto>> GetPublicAssetsAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            await EnsurePublishedWebsiteOwnershipAsync(restaurantId, ct);

            return await ProjectPublicAssetsAsync(a => a.RestaurantId == restaurantId, ct);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    public async Task<IReadOnlyList<PublicRestaurantAssetDto>> GetPublicAssetsBySlugAsync(
        string slug,
        CancellationToken ct = default)
    {
        var normalized = Domain.Entities.Websites.Website.NormalizeSlug(slug);

        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var restaurantId = await db.Websites.AsNoTracking()
                .Where(w =>
                    w.Slug == normalized &&
                    w.IsPublished &&
                    w.Status == WebsiteStatus.Published &&
                    !w.Restaurant.IsDeleted &&
                    w.Restaurant.Status != RestaurantStatus.Closed)
                .Select(w => w.RestaurantId)
                .FirstOrDefaultAsync(ct);

            if (restaurantId == Guid.Empty)
                throw new NotFoundException("Website", normalized);

            return await ProjectPublicAssetsAsync(a => a.RestaurantId == restaurantId, ct);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    private async Task<IReadOnlyList<PublicRestaurantAssetDto>> ProjectPublicAssetsAsync(
        System.Linq.Expressions.Expression<Func<RestaurantAsset, bool>> predicate,
        CancellationToken ct)
    {
        var rows = await db.RestaurantAssets.AsNoTracking()
            .Where(predicate)
            .Where(a => a.IsActive &&
                        (a.AssetType == AssetType.Logo ||
                         a.AssetType == AssetType.Favicon ||
                         a.AssetType == AssetType.CoverImage ||
                         a.AssetType == AssetType.Banner ||
                         a.AssetType == AssetType.MobileLogo))
            .OrderBy(a => a.AssetType)
            .Select(a => new
            {
                a.AssetType,
                a.File.ContentType,
                a.File.StorageKey
            })
            .ToListAsync(ct);

        return rows
            .Select(a => new PublicRestaurantAssetDto(
                a.AssetType.ToString(),
                storage.GetPresignedUrl(a.StorageKey, UrlLifetime),
                a.ContentType))
            .ToList();
    }

    // ---------------- helpers ----------------

    private static readonly TimeSpan UrlLifetime = TimeSpan.FromHours(1);

    private async Task<Guid> GetActiveTenantIdAsync(string requiredPermission, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(requiredPermission))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        var tenantOperational = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);
        if (!tenantOperational)
            throw new DomainException("TENANT_NOT_ACTIVE", "This tenant is not active.");
        await accessGuard.EnsureSubscriptionEntitledAsync(tenantId, ct);
        return tenantId;
    }

    private async Task EnsureRestaurantOwnershipAsync(Guid restaurantId, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        var belongsToTenant = await db.Restaurants.AsNoTracking()
            .AnyAsync(r => r.Id == restaurantId && r.TenantId == tenantId, ct);

        if (!belongsToTenant)
            throw new NotFoundException("Restaurant", restaurantId);
    }

    private async Task EnsurePublishedWebsiteOwnershipAsync(Guid restaurantId, CancellationToken ct)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var published = await db.Websites.AsNoTracking()
                .AnyAsync(w =>
                    w.RestaurantId == restaurantId &&
                    w.IsPublished &&
                    w.Status == WebsiteStatus.Published &&
                    !w.Restaurant.IsDeleted &&
                    w.Restaurant.Status != RestaurantStatus.Closed, ct);

            if (!published)
                throw new NotFoundException("Website", restaurantId);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    private async Task<string> GetStorageKeyAsync(Guid fileId, CancellationToken ct)
    {
        var key = await db.Files.AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => f.StorageKey)
            .FirstOrDefaultAsync(ct);

        return key ?? throw new NotFoundException("File", fileId);
    }

    private async Task TryDeletePhysicalFileAsync(string storageKey)
    {
        try
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void ValidateImageContent(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) ||
            !MenuImageRules.AllowedContentTypes.Contains(contentType))
        {
            throw new DomainException(
                "ASSET_CONTENT_TYPE_UNSUPPORTED",
                "Only JPEG, PNG, WebP, and GIF images are permitted.");
        }
    }

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static void ValidateMagicBytes(MemoryStream stream, string contentType)
    {
        Span<byte> header = stackalloc byte[12];
        var read = stream.ReadAtLeast(header, 3, throwOnEndOfStream: false);
        var bytes = header[..read];

        bool matches = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" =>
                bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" =>
                bytes.Length >= 8 && bytes[..8].SequenceEqual(PngHeader),
            "image/gif" =>
                bytes.Length >= 6 &&
                bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'8',
            "image/webp" =>
                bytes.Length >= 12 &&
                bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false
        };

        if (!matches)
            throw new DomainException(
                "ASSET_CONTENT_MISMATCH",
                "The file content does not match the declared image type.");
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "asset";

        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var extension = Path.GetExtension(name);

        if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            extension = string.Empty;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var safeStem = new string(stem.Where(char.IsLetterOrDigit).ToArray());

        if (safeStem.Length == 0)
            safeStem = "asset";

        if (safeStem.Length > 100)
            safeStem = safeStem[..100];

        return safeStem + extension;
    }

    private RestaurantAssetDto ToDto(RestaurantAsset asset, FileMetadata file) => new(
        asset.Id,
        asset.AssetType.ToString(),
        file.Id,
        file.FileName,
        file.ContentType,
        file.FileSize,
        asset.CreatedAt,
        asset.UpdatedAt,
        storage.GetPresignedUrl(file.StorageKey, UrlLifetime));
}
