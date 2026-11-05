using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Orders.DTOs;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid RestaurantId,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    OrderStatus Status,
    OrderType OrderType,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    string? SpecialInstructions,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory);

public sealed record OrderItemDto(
    Guid Id,
    Guid? MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string Currency,
    DateTime CreatedAt);

public sealed record OrderStatusHistoryDto(
    Guid Id,
    OrderStatus Status,
    string? Notes,
    DateTime CreatedAt);

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    OrderType OrderType,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);

public sealed record CreateOrderRequest(
    OrderType OrderType,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? SpecialInstructions,
    IReadOnlyList<OrderItemRequest> Items);

public sealed record OrderItemRequest(
    Guid MenuItemId,
    int Quantity);

public sealed record OrderListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public OrderStatus? Status { get; init; }
    public OrderType? OrderType { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}

public sealed record UpdateOrderStatusRequest(OrderStatus Status, string? Notes);

public sealed record CancelOrderRequest(string? Reason);

public sealed record OrderTrackingDto(string OrderNumber, OrderStatus Status);
