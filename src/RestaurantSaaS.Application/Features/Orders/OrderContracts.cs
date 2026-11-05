using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Orders.DTOs;

namespace RestaurantSaaS.Application.Features.Orders;

/// <summary>
/// Order management use cases.
///
/// All operations are scoped to the resolved tenant context; cross-tenant access
/// fails closed. TenantId and restaurant ownership are resolved server-side and
/// are never accepted as client-controlled inputs.
///
/// Order numbers, prices, tax, discounts, subtotals, and totals are calculated
/// by the backend and are never accepted from the client.
/// </summary>
public interface IOrderService
{
    /// <summary>Creates a new order directly (no cart). Server computes all financials.</summary>
    Task<OrderDto> CreateAsync(Guid restaurantId, CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the order only if it belongs to the current tenant's restaurant;
    /// otherwise throws NotFoundException (no existence leakage).
    /// </summary>
    Task<OrderDto> GetByIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Paginated, filterable order list for the given restaurant within the current tenant.</summary>
    Task<PagedResult<OrderSummaryDto>> ListAsync(Guid restaurantId, OrderListQuery query, CancellationToken ct = default);

    /// <summary>
    /// Transitions order status. Allowed transitions enforced by the domain layer.
    /// Returns the updated order.
    /// </summary>
    Task<OrderDto> UpdateStatusAsync(Guid orderId, UpdateOrderStatusRequest request, CancellationToken ct = default);

    /// <summary>Cancels an order. Domain validation rejects transitions from terminal states.</summary>
    Task CancelAsync(Guid orderId, CancelOrderRequest request, CancellationToken ct = default);

    /// <summary>Lightweight order summary projection.</summary>
    Task<OrderSummaryDto> GetSummaryAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Public order tracking by secure, unguessable tracking token.
    /// An order number must never independently authorize access to an order.
    /// </summary>
    Task<OrderTrackingDto> TrackOrderAsync(string trackingToken, CancellationToken ct = default);
}
