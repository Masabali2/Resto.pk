using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Associates an uploaded image as a restaurant logo. Only one active logo
/// per restaurant is kept; the previous logo asset is deactivated (its file
/// is not deleted, because it may be referenced elsewhere).
/// </summary>
public sealed class RestaurantLogoUploadHandler : IUploadDestinationHandler
{
    public string Kind => "restaurant-logo";

    public async Task ApplyAsync(
        FileMetadata file,
        UploadDestination destination,
        Guid userId,
        IUploadDestinationContext context,
        CancellationToken cancellationToken)
    {
        if (destination is not RestaurantLogoDestination d)
            throw new DomainException("UPLOAD_INVALID_DESTINATION",
                "Expected a restaurant-logo destination.");

        if (d.RestaurantId == Guid.Empty)
            throw new DomainException("UPLOAD_RESTAURANT_REQUIRED", "A restaurant is required.");

        var restaurant = await context.FindRestaurantAsync(d.RestaurantId, cancellationToken);
        if (restaurant is null)
            throw new DomainException("UPLOAD_RESTAURANT_NOT_FOUND",
                "The restaurant could not be found or is not accessible.");

        var isMember = await context.IsActiveMemberAsync(userId, cancellationToken);
        if (!isMember)
            throw new DomainException("UPLOAD_FORBIDDEN",
                "You are not authorized to modify this restaurant's branding.");

        var assets = await context.FindRestaurantAssetsAsync(d.RestaurantId, cancellationToken);
        foreach (var asset in assets.Where(a => a.AssetType == AssetType.Logo && a.IsActive))
            asset.SetActive(false);

        context.AddRestaurantAsset(RestaurantAsset.Create(d.RestaurantId, file.Id, AssetType.Logo));
    }
}
