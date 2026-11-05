using RestaurantSaaS.Application.Features.PublicOrders.DTOs;

namespace RestaurantSaaS.Application.Features.PublicOrders;

/// <summary>
/// Public order creation and tracking for anonymous customers.
///
/// Creates an order from the current shopper cart. The order is scoped to
/// the resolved restaurant and shopper session. All financials are
/// server-calculated. A secure tracking token is generated and its hash
/// stored; the raw token is returned exactly once in the response.
///
/// Tracking lookup resolves an order by the SHA-256 hash of the raw tracking
/// token. The raw token is never stored or returned by tracking.
/// </summary>
public interface IPublicOrderService
{
    /// <summary>
    /// Creates an order from the current shopper's cart at the given restaurant.
    /// Returns the order details including the raw tracking token (once only).
    /// </summary>
    Task<CreatePublicOrderResponse> CreateOrderFromCartAsync(
        Guid restaurantId,
        CreatePublicOrderRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up an order for public tracking by the raw tracking token.
    ///
    /// The raw token is SHA-256 hashed and compared against the persisted
    /// TrackingTokenHash. Returns null if no matching order exists
    /// (no existence leakage).
    /// </summary>
    Task<PublicOrderTrackingDto?> GetOrderByTrackingTokenAsync(
        Guid restaurantId,
        string rawTrackingToken,
        CancellationToken ct = default);
}
