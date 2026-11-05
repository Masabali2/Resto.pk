using RestaurantSaaS.Application.Features.Carts.DTOs;
using RestaurantSaaS.Application.Features.Orders.DTOs;

namespace RestaurantSaaS.Application.Features.Carts;

/// <summary>
/// Cart management use cases for customer ordering.
///
/// A cart is always scoped to a single restaurant within the resolved tenant.
/// Cross-restaurant cart items are prevented by server-side business logic.
///
/// TenantId and restaurant ownership are resolved server-side and are never
/// accepted as client-controlled inputs. Cart prices are server-determined;
/// clients never supply unit prices, line totals, or financial totals.
/// </summary>
public interface ICartService
{
    /// <summary>
    /// Retrieves the active cart for the current customer/session at the given restaurant.
    /// Returns an empty cart if none exists yet.
    /// </summary>
    Task<CartDto> GetCurrentCartAsync(Guid restaurantId, CancellationToken ct = default);

    /// <summary>Adds a menu item to the cart. Unit price is server-determined.</summary>
    Task<CartItemDto> AddItemAsync(Guid restaurantId, AddCartItemRequest request, CancellationToken ct = default);

    /// <summary>Updates the quantity of an existing cart item.</summary>
    Task<CartItemDto> UpdateItemAsync(Guid restaurantId, Guid cartItemId, UpdateCartItemRequest request, CancellationToken ct = default);

    /// <summary>Removes a cart item.</summary>
    Task RemoveItemAsync(Guid restaurantId, Guid cartItemId, CancellationToken ct = default);

    /// <summary>Removes all items from the cart.</summary>
    Task ClearAsync(Guid restaurantId, CancellationToken ct = default);

    /// <summary>Converts the cart into an order and marks the cart converted.</summary>
    Task<OrderDto> CheckoutAsync(Guid restaurantId, CheckoutCartRequest request, CancellationToken ct = default);
}
