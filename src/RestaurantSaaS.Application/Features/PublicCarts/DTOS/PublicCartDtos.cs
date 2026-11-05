using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.PublicCarts.DTOs;

/// <summary>
/// Public cart response — excludes TenantId, SessionHash, and other internal IDs.
/// Only data appropriate for an anonymous customer.
/// </summary>
public sealed record PublicCartDto(
    Guid Id,
    Guid RestaurantId,
    int ItemCount,
    decimal TotalAmount,
    string Currency,
    DateTime ExpiresAt,
    IReadOnlyList<PublicCartItemDto> Items);

/// <summary>
/// Public cart line item — server-calculated prices only.
/// </summary>
public sealed record PublicCartItemDto(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    string Currency,
    decimal LineTotal);

/// <summary>
/// Request to add an item to the public cart. No price accepted from client.
/// </summary>
public sealed record AddPublicCartItemRequest(Guid MenuItemId, int Quantity);

/// <summary>
/// Request to update a public cart item quantity.
/// </summary>
public sealed record UpdatePublicCartItemRequest(int Quantity);
