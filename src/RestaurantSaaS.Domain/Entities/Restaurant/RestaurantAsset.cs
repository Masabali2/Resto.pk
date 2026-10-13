
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Restaurants;

public class RestaurantAsset : AuditableEntity
{
    public Guid RestaurantId { get; private set; }

    public Guid FileId { get; private set; }

    public AssetType AssetType { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Restaurant Restaurant { get; private set; } = null!;

    public Files.FileMetadata File { get; private set; } = null!;

    private RestaurantAsset()
    {
    }

    public static RestaurantAsset Create(
        Guid restaurantId,
        Guid fileId,
        AssetType assetType)
    {
        if (restaurantId == Guid.Empty)
            throw new DomainException(
                "ASSET_RESTAURANT_REQUIRED",
                "Asset requires a restaurant.");

        if (fileId == Guid.Empty)
            throw new DomainException(
                "ASSET_FILE_REQUIRED",
                "Asset requires a file.");

        if (!Enum.IsDefined(assetType))
            throw new DomainException(
                "ASSET_TYPE_INVALID",
                "The specified asset type is invalid.");

        var now = DateTime.UtcNow;

        return new RestaurantAsset
        {
            RestaurantId = restaurantId,
            FileId = fileId,
            AssetType = assetType,
            IsActive = true,
            CreatedAt = now
        };
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }
}