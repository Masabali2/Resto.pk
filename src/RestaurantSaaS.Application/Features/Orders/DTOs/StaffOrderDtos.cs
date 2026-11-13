using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Orders.DTOS;

/// <summary>
/// Staff-facing order line item. Includes server-authoritative pricing
/// since staff are authenticated and authorized within the tenant.
/// </summary>
public sealed record StaffOrderItemDto(
    Guid? MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string Currency);

/// <summary>
/// Staff-facing order details. Includes tenant-scoped financial and customer
/// data. Never exposes tracking-token hashes or other security values.
/// </summary>
public sealed record StaffOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    OrderType OrderType,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    string? SpecialInstructions,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    IReadOnlyList<StaffOrderItemDto> Items);
/// <summary>
/// Aggregated order summary for a restaurant within an optional date range.
///
/// All monetary figures follow the existing business rule: revenue is the sum
/// of `TotalAmount` for completed orders, independent of `PaymentStatus`.
/// Date boundaries are evaluated in the restaurant's timezone.
/// </summary>
public sealed record StaffOrderSummaryDto(
    int TotalOrders,
    int PendingKitchen,
    int CompletedToday,
    int CompletedOrders,
    int CancelledOrders,
    decimal TotalRevenue,
    decimal AverageOrderValue,
    string Currency,
    DateOnly? StartDate,
    DateOnly? EndDate);
/// <summary>
/// A single daily bucket of order analytics.
/// </summary>
public sealed record DailyOrderDto(DateOnly Date, int OrderCount, decimal Revenue);

/// <summary>
/// Order trend analytics for a restaurant within an optional date range.
/// Returned per calendar day in the restaurant's timezone.
/// </summary>
public sealed record OrderAnalyticsDto(
    IReadOnlyList<DailyOrderDto> Daily,
    int TotalOrders,
    decimal TotalRevenue);
/// <summary>
/// Request to transition an order to a new status. The domain layer enforces
/// legal transitions (Order.CanTransitionTo). The changed-by user is recorded in
/// the order's status history. The order must belong to the specified restaurant.
/// </summary>
public sealed record UpdateOrderStatusRequest(OrderStatus NewStatus, string? Notes);
