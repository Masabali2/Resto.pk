using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Public order creation for anonymous customers.
///
/// Supports both cart-based checkout and direct order creation without a cart.
/// All financials are server-calculated. A secure tracking token is generated
/// and its SHA-256 hash stored in Order.TrackingTokenHash; the raw token is
/// returned exactly once in the response and is never persisted or logged.
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

        // Duplicate prevention: cart already converted or otherwise inactive
        if (cart.Status != CartStatus.Active)
            throw new DomainException("CART_ALREADY_CONVERTED", "This cart has already been used to create an order.");

        cart.EnsureUsable(utcNow);

        if (cart.Items.Count == 0)
            throw new DomainException("CART_EMPTY", "Cannot create an order from an empty cart.");

        var restaurant = await GetRestaurantAsync(restaurantId, tenantId, ct);

        var menuItemIds = cart.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await LoadMenuItems(menuItemIds, ct);

        var menuItemLookup = menuItems.ToDictionary(mi => mi.Id);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;
        string? currency = null;

        foreach (var cartItem in cart.Items)
        {
            if (!menuItemLookup.TryGetValue(cartItem.MenuItemId, out var menuItem))
                throw new NotFoundException("MenuItem", cartItem.MenuItemId);

            ValidateMenuItemOwnership(menuItem, restaurantId);
            ValidateMenuItemOrderable(menuItem);

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
                Guid.NewGuid(),
                cartItem.MenuItemId,
                menuItem.Name,
                price.Amount,
                cartItem.Quantity,
                price.Currency));
        }

        var financials = CalculateFinancials(subtotal, restaurant.TaxRate);

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
            financials.Subtotal,
            financials.DiscountAmount,
            financials.TaxAmount,
            financials.TotalAmount,
            currency ?? Money.DefaultCurrency,
            request.SpecialInstructions,
            request.OrderType,
            orderItems);

        order.SetTrackingTokenHash(trackingTokenHash);

        db.Orders.Add(order);

        cart.MarkConverted(utcNow);
        foreach (var item in cart.Items.ToList())
        {
            db.Entry(item).State = EntityState.Deleted;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Optimistic concurrency on Cart.RowVersion: another checkout
            // already converted this cart. Fail closed with a controlled error.
            throw new DomainException("CART_ALREADY_CONVERTED", "This cart has already been used to create an order.");
        }

        return MapToResponse(order, rawTrackingToken, orderItems);
    }

    public async Task<CreatePublicOrderResponse> CreateDirectOrderAsync(
        Guid restaurantId,
        CreateDirectPublicOrderRequest request,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var sessionHash = GetSessionHash();

        var restaurant = await GetRestaurantAsync(restaurantId, tenantId, ct);

        if (!restaurant.CanAcceptOrders)
            throw new DomainException("RESTAURANT_NOT_ACTIVE", "Restaurant is not accepting orders.");

        if (request.Items is null || request.Items.Count == 0)
            throw new DomainException("ORDER_ITEMS_REQUIRED", "An order must have at least one item.");

        if (request.Items.Any(i => i is null))
            throw new DomainException("ORDER_ITEM_INVALID", "Order item cannot be null.");

        var distinctItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        if (distinctItemIds.Count != request.Items.Count)
            throw new DomainException("DUPLICATE_MENU_ITEM", "Duplicate menu item IDs are not allowed. Use quantity instead.");

        var menuItems = await LoadMenuItems(distinctItemIds, ct);

        var menuItemLookup = menuItems.ToDictionary(mi => mi.Id);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;
        string? currency = null;

        foreach (var itemRequest in request.Items)
        {
            if (!menuItemLookup.TryGetValue(itemRequest.MenuItemId, out var menuItem))
                throw new NotFoundException("MenuItem", itemRequest.MenuItemId);

            ValidateMenuItemOwnership(menuItem, restaurantId);
            ValidateMenuItemOrderable(menuItem);

            var price = menuItem.GetCurrentPrice();

            if (currency is null)
                currency = price.Currency;
            else if (currency != price.Currency)
                throw new DomainException(
                    "CURRENCY_MISMATCH",
                    "All items in an order must use the same currency.");

            var lineTotal = price.Amount * itemRequest.Quantity;
            subtotal += lineTotal;

            orderItems.Add(OrderItem.CreateSnapshot(
                Guid.NewGuid(),
                itemRequest.MenuItemId,
                menuItem.Name,
                price.Amount,
                itemRequest.Quantity,
                price.Currency));
        }

        var financials = CalculateFinancials(subtotal, restaurant.TaxRate);

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
            financials.Subtotal,
            financials.DiscountAmount,
            financials.TaxAmount,
            financials.TotalAmount,
            currency ?? Money.DefaultCurrency,
            request.SpecialInstructions,
            request.OrderType,
            orderItems);

        order.SetTrackingTokenHash(trackingTokenHash);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return MapToResponse(order, rawTrackingToken, orderItems);
    }

    // ------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------

    private static (decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount) CalculateFinancials(
        decimal subtotal,
        decimal taxRate)
    {
        var discountAmount = 0m;
        var taxAmount = Math.Round(subtotal * taxRate / 100m, 4, MidpointRounding.AwayFromZero);
        var totalAmount = subtotal - discountAmount + taxAmount;

        return (subtotal, discountAmount, taxAmount, totalAmount);
    }

    private static CreatePublicOrderResponse MapToResponse(
        Order order,
        string rawTrackingToken,
        IReadOnlyList<OrderItem> orderItems)
    {
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

    private async Task<Restaurant> GetRestaurantAsync(
        Guid restaurantId,
        Guid tenantId,
        CancellationToken ct)
    {
        var restaurant = await db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Restaurant", restaurantId);

        return restaurant;
    }

    private async Task<List<MenuItem>> LoadMenuItems(
        List<Guid> menuItemIds,
        CancellationToken ct)
    {
        return await db.MenuItems
            .AsNoTracking()
            .Include(mi => mi.Category)
            .ThenInclude(c => c.Menu)
            .Where(mi => menuItemIds.Contains(mi.Id))
            .ToListAsync(ct);
    }

    private static void ValidateMenuItemOwnership(MenuItem menuItem, Guid restaurantId)
    {
        if (menuItem.Category is null ||
            menuItem.Category.Menu is null ||
            menuItem.Category.Menu.RestaurantId != restaurantId)
        {
            throw new CrossTenantAccessException();
        }
    }

    private static void ValidateMenuItemOrderable(MenuItem menuItem)
    {
        if (!menuItem.CanBeOrdered)
            throw new DomainException(
                "ITEM_NOT_ORDERABLE",
                $"Menu item '{menuItem.Name}' is no longer available for ordering.");
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
