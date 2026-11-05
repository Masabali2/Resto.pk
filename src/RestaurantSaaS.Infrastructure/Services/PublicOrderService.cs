using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Public order creation for anonymous customers.
///
/// Creates an order from the current shopper cart. All financials are
/// server-calculated. A secure tracking token is generated and its SHA-256
/// hash stored in Order.TrackingTokenHash; the raw token is returned exactly
/// once in the response and is never persisted or logged.
/// </summary>
public sealed class PublicOrderService(
    ApplicationDbContext db,
    IShopperSessionContext sessionContext,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IOrderNumberGenerator orderNumberGenerator,
    IOrderTrackingTokenService trackingTokenService) : IPublicOrderService
{
    public async Task<CreatePublicOrderResponse> CreateOrderFromCartAsync(
        Guid restaurantId,
        CreatePublicOrderRequest request,
        CancellationToken ct = default)
    {
        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();
        var utcNow = dateTimeProvider.UtcNow;

        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c =>
                c.SessionId == sessionHash &&
                c.RestaurantId == restaurantId,
            ct)
            ?? throw new NotFoundException("Cart", "No active cart found for this session and restaurant.");

        cart.EnsureUsable(utcNow);

        if (cart.Items.Count == 0)
            throw new DomainException("CART_EMPTY", "Cannot create an order from an empty cart.");

        var menuItemIds = cart.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await db.MenuItems
            .AsNoTracking()
            .Include(mi => mi.Category)
            .ThenInclude(c => c.Menu)
            .Where(mi => menuItemIds.Contains(mi.Id))
            .ToListAsync(ct);

        var menuItemLookup = menuItems.ToDictionary(mi => mi.Id);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;
        string? currency = null;

        foreach (var cartItem in cart.Items)
        {
            if (!menuItemLookup.TryGetValue(cartItem.MenuItemId, out var menuItem))
                throw new NotFoundException("MenuItem", cartItem.MenuItemId);

            if (menuItem.Category is null ||
                menuItem.Category.Menu is null ||
                menuItem.Category.Menu.RestaurantId != restaurantId)
            {
                throw new CrossTenantAccessException();
            }

            if (!menuItem.CanBeOrdered)
                throw new DomainException(
                    "ITEM_NOT_ORDERABLE",
                    $"Menu item '{menuItem.Name}' is no longer available for ordering.");

            var price = menuItem.GetCurrentPrice();

            if (currency is null)
                currency = price.Currency;
            else if (currency != price.Currency)
                throw new DomainException(
                    "CURRENCY_MISMATCH",
                    "All items in an order must use the same currency.");

            var lineTotal = price.Amount * cartItem.Quantity;
            subtotal += lineTotal;

            orderItems.Add(OrderItem.CreateSnapshot(
                Guid.NewGuid(), // placeholder; Order.Create() overwrites with the real Order.Id
                cartItem.MenuItemId,
                menuItem.Name,
                price.Amount,
                cartItem.Quantity,
                price.Currency));
        }

        currency ??= Money.DefaultCurrency;

        var discountAmount = 0m;
        var taxAmount = 0m;
        var totalAmount = subtotal - discountAmount + taxAmount;

        var orderNumber = orderNumberGenerator.Generate();
        var (rawTrackingToken, trackingTokenHash) = trackingTokenService.Generate();

        var order = Order.Create(
            tenantId,
            restaurantId,
            null,
            orderNumber,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            subtotal,
            discountAmount,
            taxAmount,
            totalAmount,
            currency,
            request.SpecialInstructions,
            OrderType.Pickup,
            orderItems);

        order.SetTrackingTokenHash(trackingTokenHash);

        db.Orders.Add(order);

        cart.MarkConverted(utcNow);
        foreach (var item in cart.Items.ToList())
        {
            db.Entry(item).State = EntityState.Deleted;
        }

        await db.SaveChangesAsync(ct);

        return new CreatePublicOrderResponse(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.Currency,
            rawTrackingToken,
            orderItems.Select(oi => new PublicOrderItemDto(
                oi.MenuItemId,
                oi.ItemName,
                oi.UnitPrice,
                oi.Quantity,
                oi.LineTotal,
                oi.Currency)).ToList(),
            order.CreatedAt);
    }

    private string GetSessionHash()
    {
        var hash = sessionContext.SessionHash;
        if (string.IsNullOrWhiteSpace(hash))
            throw new NotAuthenticatedException("CART_SESSION_REQUIRED", "A shopper session is required.");
        return hash;
    }

    private Guid GetTenantId()
    {
        if (!tenantContext.HasTenant)
            throw new CrossTenantAccessException();
        return tenantContext.GetRequiredTenantId();
    }

    public async Task<PublicOrderTrackingDto?> GetOrderByTrackingTokenAsync(
        Guid restaurantId,
        string rawTrackingToken,
        CancellationToken ct = default)
    {
        // Hash the raw token and compare against the persisted hash.
        // Never log or return the raw token.
        var trackingTokenHash = trackingTokenService.Hash(rawTrackingToken);

        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o =>
                o.TrackingTokenHash == trackingTokenHash &&
                o.RestaurantId == restaurantId,
            ct)
            ?? null;

        if (order is null)
            return null;

        return MapToTrackingDto(order);
    }

    private static PublicOrderTrackingDto MapToTrackingDto(Order order)
    {
        return new PublicOrderTrackingDto(
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.Currency,
            order.Items.Select(i => new PublicTrackingOrderItemDto(
                i.ItemName,
                i.Quantity)).ToList(),
            order.CreatedAt,
            order.CompletedAt);
    }
}
