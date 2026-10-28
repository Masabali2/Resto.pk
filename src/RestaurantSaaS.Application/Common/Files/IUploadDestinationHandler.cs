using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Application.Common.Files;

/// <summary>
/// Read/write access to upload-destination resources, exposed to destination
/// handlers so they can authorize and apply associations without the handler
/// knowing about EF Core. Implementations share the service's DbContext so
/// entities remain tracked and a single SaveChanges commits everything.
/// All lookups are scoped to the tenant supplied at construction.
/// </summary>
public interface IUploadDestinationContext
{
    /// <summary>Resolved tenant for the current request.</summary>
    Guid TenantId { get; }

    Task<Website?> FindWebsiteAsync(Guid websiteId, CancellationToken cancellationToken);

    Task<Restaurant?> FindRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken);

    Task<MenuItem?> FindMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken);

    Task<bool> IsActiveMemberAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked restaurant branding assets.</summary>
    Task<List<RestaurantAsset>> FindRestaurantAssetsAsync(
        Guid restaurantId, CancellationToken cancellationToken);

    /// <summary>Starts tracking a new restaurant asset.</summary>
    void AddRestaurantAsset(RestaurantAsset asset);
}

/// <summary>
/// Destination-specific upload assignment handler. Each handler validates
/// authorization for exactly one destination kind and applies the file
/// association to tracked entities via <paramref name="context"/>.
/// </summary>
public interface IUploadDestinationHandler
{
    string Kind { get; }

    Task ApplyAsync(
        FileMetadata file,
        UploadDestination destination,
        Guid userId,
        IUploadDestinationContext context,
        CancellationToken cancellationToken);
}
