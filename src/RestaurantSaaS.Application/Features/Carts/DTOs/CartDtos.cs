using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Carts.DTOs;

public sealed record CartDto(
    Guid Id,
    Guid RestaurantId,
    Guid? CustomerId,
    CartStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<CartItemDto> Items);

public sealed record CartItemDto(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    string Currency,
    decimal LineTotal,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AddCartItemRequest(Guid MenuItemId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

public sealed record CheckoutCartRequest(
    OrderType OrderType,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? SpecialInstructions);
