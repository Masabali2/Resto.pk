using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Restaurant branding asset use cases. All operations are scoped to the
/// resolved tenant context; cross-tenant access fails closed.
/// </summary>
public interface IRestaurantAssetService
{
    Task<IReadOnlyList<RestaurantAssetDto>> ListAsync(Guid restaurantId, CancellationToken ct = default);

    Task<RestaurantAssetDto> UploadAsync(
        Guid restaurantId,
        string assetType,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an asset when it is no longer referenced. Returns true when
    /// deleted, false when the asset is still referenced and must not be
    /// deleted. Throws NotFoundException when the asset does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid restaurantId, Guid assetId, CancellationToken ct = default);

    Task<IReadOnlyList<PublicRestaurantAssetDto>> GetPublicAssetsAsync(
        Guid restaurantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PublicRestaurantAssetDto>> GetPublicAssetsBySlugAsync(
        string slug,
        CancellationToken ct = default);

    /// <summary>Authoritative storage usage for a restaurant.</summary>
    Task<StorageUsageDto> GetStorageUsageAsync(Guid restaurantId, CancellationToken ct = default);
}
