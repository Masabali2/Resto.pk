using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.PublicOrders.DTOs;

/// <summary>
/// Request to create a public order from the current shopper cart.
/// No prices, totals, or item lists accepted from the client — all financials
/// are server-calculated from cart data.
/// </summary>
public sealed record CreatePublicOrderRequest(
    OrderType OrderType,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? SpecialInstructions);

/// <summary>
/// Response from public order creation. The raw tracking token is included
/// exactly once and must never be stored or returned again.
/// </summary>
public sealed record CreatePublicOrderResponse(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    string RawTrackingToken,
    IReadOnlyList<PublicOrderItemDto> Items,
    DateTime CreatedAt);

/// <summary>
/// Public order line item — server-authoritative snapshot.
/// </summary>
public sealed record PublicOrderItemDto(
    Guid? MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string Currency);

/// <summary>
/// Public order tracking response — only data safe to expose to an anonymous
/// customer who holds a valid tracking token. No TenantId, CustomerId, or
/// shopper-session identifiers.
/// </summary>
public sealed record PublicOrderTrackingDto(
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<PublicTrackingOrderItemDto> Items,
    DateTime CreatedAt,
    DateTime? CompletedAt);

/// <summary>
/// Public tracking line item — item name and quantity only, no pricing.
/// </summary>
public sealed record PublicTrackingOrderItemDto(
    string ItemName,
    int Quantity);
