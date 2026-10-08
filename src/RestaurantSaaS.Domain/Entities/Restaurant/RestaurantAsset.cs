using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Restaurants;

/// <summary>
/// A file (logo/banner/…) attached to a restaurant.
///
/// DESIGN NOTE (requirement #38): No TenantId here. Tenancy derives through
/// RestaurantAsset -> Restaurant -> Tenant, enforced by the DB-level chain.
/// Avoiding redundant TenantId prevents drift where asset and restaurant
/// disagree about ownership.
/// </summary>
public class RestaurantAsset : AuditableEntity
{
    public Guid RestaurantId { get; private set; }

    /// <summary>Reference into FileMetadata (storage metadata row).</summary>
    public Guid FileId { get; private set; }

    public AssetType AssetType { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Restaurant Restaurant { get; private set; } = null!;
    public Files.FileMetadata File { get; private set; } = null!;

    public static RestaurantAsset Create(Guid restaurantId, Guid fileId, AssetType assetType)
    {
        if (restaurantId == Guid.Empty)
            throw new DomainException("ASSET_RESTAURANT_REQUIRED", "Asset requires a restaurant.");

        if (fileId == Guid.Empty)
            throw new DomainException("ASSET_FILE_REQUIRED", "Asset requires a file.");

        return new RestaurantAsset
        {
            RestaurantId = restaurantId,
            FileId = fileId,
            AssetType = assetType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }
}
