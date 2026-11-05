using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Application.Features.Restaurants;

/// <summary>
/// Public restaurant resolution for anonymous customer ordering.
///
/// All checks are performed server-side. The caller supplies only a website
/// slug; tenant and restaurant ownership are never accepted from the client.
/// </summary>
public interface IPublicRestaurantService
{
    /// <summary>
    /// Resolves a restaurant that is open for public ordering via a published
    /// website slug.
    ///
    /// Verifies:
    ///  - The website exists, is published and active.
    ///  - The restaurant exists, belongs to the website's tenant, and
    ///    <see cref="Domain.Entities.Restaurants.Restaurant.CanAcceptOrders"/>.
    ///  - The tenant is active and not deleted.
    ///  - Subscription entitlement (if required for public ordering).
    ///
    /// Returns null if the restaurant cannot be resolved for ordering
    /// (no existence leakage beyond a not-found result).
    /// </summary>
    Task<PublicOrderingRestaurantDto?> GetValidRestaurantForOrderingAsync(
        string websiteSlug,
        CancellationToken ct = default);
}
