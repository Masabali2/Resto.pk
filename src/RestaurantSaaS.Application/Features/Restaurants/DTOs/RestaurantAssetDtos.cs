namespace RestaurantSaaS.Application.Features.Restaurants.DTOs;

public sealed record RestaurantAssetDto(
    Guid Id,
    string AssetType,
    Guid FileId,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Url);

public sealed record PublicRestaurantAssetDto(
    string AssetType,
    string Url,
    string ContentType);

/// <summary>Authoritative storage usage for a restaurant.</summary>
/// <remarks>
/// QuotaBytes is null when no authoritative storage quota policy is configured.
/// Callers must not fabricate a limit.
/// </remarks>
public sealed record StorageUsageDto(long UsedBytes, long? QuotaBytes);
