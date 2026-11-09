using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Orders;

/// <summary>
/// Authenticated staff order management.
///
/// All operations are tenant-scoped: the tenant context is resolved server-side
/// from the authenticated user's StaffMembership. Staff can only see and modify
/// orders belonging to their tenant's restaurants. The shopper session is never
/// used as an authorization mechanism for staff endpoints.
/// </summary>
public interface IStaffOrderService
{
    /// <summary>
    /// Lists orders for the current tenant, optionally scoped to a restaurant.
    /// </summary>
    Task<PagedResult<StaffOrderDto>> ListAsync(
        Guid? restaurantId,
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Gets order details for staff. Returns null if the order does not belong
    /// to the current tenant's restaurant (no existence leakage across tenants or restaurants).
    /// </summary>
    Task<StaffOrderDto?> GetByIdAsync(
        Guid restaurantId,
        Guid orderId,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions an order to a new status. The domain layer enforces legal
    /// transitions (Order.CanTransitionTo). The changed-by user is recorded in
    /// the order's status history. The order must belong to the specified restaurant.
    /// </summary>
    Task<StaffOrderDto> TransitionStatusAsync(
        Guid restaurantId,
        Guid orderId,
        UpdateOrderStatusRequest request,
        Guid changedByUserId,
        CancellationToken ct = default);
    Task<StaffOrderSummaryDto> GetSummaryAsync(
    Guid restaurantId,
    CancellationToken ct = default);
}
