using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;

namespace RestaurantSaaS.Application.Features.PublicCarts;

/// <summary>
/// Public (anonymous) cart operations for customer ordering.
///
/// The cart is identified by the resolved restaurant and the current shopper
/// session hash. TenantId, restaurant ownership, and session identity are
/// resolved server-side — never accepted from the client.
///
/// All prices are server-determined from menu data. Clients never supply
/// unit prices, line totals, or financial totals.
/// </summary>
public interface IPublicCartService
{
    /// <summary>
    /// Gets or creates the active cart for the current shopper session at the
    /// resolved restaurant. Returns an empty cart if none exists yet.
    /// </summary>
    Task<PublicCartDto> GetCurrentCartAsync(Guid restaurantId, CancellationToken ct = default);

    /// <summary>
    /// Adds a menu item to the cart. Unit price is server-determined from menu data.
    /// </summary>
    Task<PublicCartItemDto> AddItemAsync(
        Guid restaurantId,
        AddPublicCartItemRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the quantity of an existing cart item.
    /// </summary>
    Task<PublicCartItemDto> UpdateItemAsync(
        Guid restaurantId,
        Guid cartItemId,
        UpdatePublicCartItemRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a cart item.
    /// </summary>
    Task RemoveItemAsync(Guid restaurantId, Guid cartItemId, CancellationToken ct = default);

    /// <summary>
    /// Removes all items from the cart.
    /// </summary>
    Task ClearAsync(Guid restaurantId, CancellationToken ct = default);
}
