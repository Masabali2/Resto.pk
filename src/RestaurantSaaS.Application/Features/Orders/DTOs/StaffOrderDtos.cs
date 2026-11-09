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
public sealed record StaffOrderSummaryDto(
    int TotalOrders,
    int PendingKitchen,
    int CompletedToday,
    decimal TotalRevenue,
    string Currency);
/// <summary>
/// Request to transition an order to a new status. The domain layer enforces
/// legal transitions (Order.CanTransitionTo).
/// </summary>
public sealed record UpdateOrderStatusRequest(OrderStatus NewStatus, string? Notes);
