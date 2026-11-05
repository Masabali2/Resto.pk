using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.PublicCarts;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Public cart service for anonymous customer ordering.
///
/// Cart identity is the combination of the resolved restaurant and the
/// shopper session hash. TenantId, restaurant ownership, and session identity
/// are resolved server-side — never accepted from the client.
/// </summary>
public sealed class PublicCartService(
    ApplicationDbContext db,
    IShopperSessionContext sessionContext,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider) : IPublicCartService
{
    public async Task<PublicCartDto> GetCurrentCartAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();

        var cart = await GetOrCreateCartAsync(restaurantId, tenantId, sessionHash, dateTimeProvider.UtcNow, ct);

        return await MapToDtoAsync(cart, ct);
    }

    public async Task<PublicCartItemDto> AddItemAsync(
        Guid restaurantId,
        AddPublicCartItemRequest request,
        CancellationToken ct = default)
    {
        if (request.Quantity < 1 || request.Quantity > CartItem.MaxQuantityPerLine)
            throw new DomainException(
                "CARTITEM_QUANTITY_INVALID",
                $"Quantity must be between 1 and {CartItem.MaxQuantityPerLine}.");

        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();

        var cart = await GetOrCreateCartAsync(restaurantId, tenantId, sessionHash, dateTimeProvider.UtcNow, ct);

        var menuItem = await db.MenuItems
            .AsNoTracking()
            .Include(mi => mi.Category)
            .ThenInclude(c => c.Menu)
            .FirstOrDefaultAsync(mi => mi.Id == request.MenuItemId, ct)
            ?? throw new NotFoundException("MenuItem", request.MenuItemId);

        if (menuItem.Category is null ||
            menuItem.Category.Menu is null ||
            menuItem.Category.Menu.RestaurantId != restaurantId)
        {
            throw new CrossTenantAccessException();
        }

        if (!menuItem.CanBeOrdered)
            throw new DomainException(
                "ITEM_NOT_ORDERABLE",
                "This menu item is not available for ordering.");

        var price = menuItem.GetCurrentPrice();

        var existing = cart.Items.FirstOrDefault(i => i.MenuItemId == request.MenuItemId);

        if (existing is not null)
        {
            var newQuantity = existing.Quantity + request.Quantity;
            if (newQuantity > CartItem.MaxQuantityPerLine)
                throw new DomainException(
                    "CARTITEM_QUANTITY_INVALID",
                    $"Total quantity cannot exceed {CartItem.MaxQuantityPerLine}.");

            existing.ChangeQuantity(newQuantity, dateTimeProvider.UtcNow);
            existing.RefreshServerPrice(price.Amount, price.Currency, dateTimeProvider.UtcNow);
        }
        else
        {
            var newItem = CartItem.Create(
                cart.Id,
                request.MenuItemId,
                request.Quantity,
                price.Amount,
                price.Currency);
            db.CartItems.Add(newItem);
        }

        await db.SaveChangesAsync(ct);

        var updated = await GetCurrentCartAsync(restaurantId, ct);
        return updated.Items.First(i => i.MenuItemId == request.MenuItemId);
    }

    public async Task<PublicCartItemDto> UpdateItemAsync(
        Guid restaurantId,
        Guid cartItemId,
        UpdatePublicCartItemRequest request,
        CancellationToken ct = default)
    {
        if (request.Quantity < 1 || request.Quantity > CartItem.MaxQuantityPerLine)
            throw new DomainException(
                "CARTITEM_QUANTITY_INVALID",
                $"Quantity must be between 1 and {CartItem.MaxQuantityPerLine}.");

        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();

        var cart = await GetOrCreateCartAsync(restaurantId, tenantId, sessionHash, dateTimeProvider.UtcNow, ct);

        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId)
            ?? throw new NotFoundException("CartItem", cartItemId);

        var menuItem = await db.MenuItems
            .AsNoTracking()
            .Include(mi => mi.Category)
            .ThenInclude(c => c.Menu)
            .FirstOrDefaultAsync(mi => mi.Id == item.MenuItemId, ct)
            ?? throw new NotFoundException("MenuItem", item.MenuItemId);

        if (menuItem.Category is null ||
            menuItem.Category.Menu is null ||
            menuItem.Category.Menu.RestaurantId != restaurantId)
        {
            throw new CrossTenantAccessException();
        }

        if (!menuItem.CanBeOrdered)
            throw new DomainException(
                "ITEM_NOT_ORDERABLE",
                "This menu item is no longer available for ordering.");

        var price = menuItem.GetCurrentPrice();
        item.ChangeQuantity(request.Quantity, dateTimeProvider.UtcNow);
        item.RefreshServerPrice(price.Amount, price.Currency, dateTimeProvider.UtcNow);

        await db.SaveChangesAsync(ct);

        var updated = await GetCurrentCartAsync(restaurantId, ct);
        return updated.Items.First(i => i.Id == cartItemId);
    }

    public async Task RemoveItemAsync(
        Guid restaurantId,
        Guid cartItemId,
        CancellationToken ct = default)
    {
        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();

        var cart = await GetOrCreateCartAsync(restaurantId, tenantId, sessionHash, dateTimeProvider.UtcNow, ct);

        var item = cart.Items.FirstOrDefault(i => i.Id == cartItemId)
            ?? throw new NotFoundException("CartItem", cartItemId);

        db.Entry(item).State = EntityState.Deleted;

        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var sessionHash = GetSessionHash();
        var tenantId = GetTenantId();

        var cart = await GetOrCreateCartAsync(restaurantId, tenantId, sessionHash, dateTimeProvider.UtcNow, ct);

        foreach (var item in cart.Items.ToList())
        {
            db.Entry(item).State = EntityState.Deleted;
        }

        await db.SaveChangesAsync(ct);
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

    private async Task<Cart> GetOrCreateCartAsync(
        Guid restaurantId,
        Guid tenantId,
        string sessionHash,
        DateTime utcNow,
        CancellationToken ct)
    {
        // Use AsNoTracking to avoid change-tracker returning a cart from a
        // different shopper session when both services share the same DbContext.
        var cart = await db.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c =>
                c.SessionId == sessionHash &&
                c.RestaurantId == restaurantId,
            ct);

        if (cart is not null)
        {
            // Reload with tracking so modifications (Renew, MarkConverted) are persisted.
            cart = await db.Carts
                .Include(c => c.Items)
                .FirstAsync(c => c.Id == cart.Id, ct);

            cart.EnsureUsable(utcNow);
            cart.Renew(utcNow);
            await db.SaveChangesAsync(ct);
            return cart;
        }

        var newCart = Cart.Create(tenantId, restaurantId, null, sessionHash, utcNow);
        db.Carts.Add(newCart);
        await db.SaveChangesAsync(ct);

        return await db.Carts
            .Include(c => c.Items)
            .FirstAsync(c => c.Id == newCart.Id, ct);
    }

    private async Task<PublicCartDto> MapToDtoAsync(Cart cart, CancellationToken ct)
    {
        var menuItemIds = cart.Items.Select(i => i.MenuItemId).ToList();
        var menuItemNames = await db.MenuItems
            .AsNoTracking()
            .Where(mi => menuItemIds.Contains(mi.Id))
            .Select(mi => new { mi.Id, mi.Name })
            .ToDictionaryAsync(mi => mi.Id, mi => mi.Name, ct);

        return new PublicCartDto(
            cart.Id,
            cart.RestaurantId ?? Guid.Empty,
            cart.Items.Count,
            cart.Items.Sum(i => i.GetLineTotal()),
            cart.Items.Count > 0 ? cart.Items.First().Currency : Money.DefaultCurrency,
            cart.ExpiresAt,
            cart.Items.Select(i => MapItemToDto(i, menuItemNames)).ToList()
        );
    }

    private static PublicCartItemDto MapItemToDto(
        CartItem item,
        IReadOnlyDictionary<Guid, string> menuItemNames)
    {
        var itemName = menuItemNames.TryGetValue(item.MenuItemId, out var name)
            ? name
            : "Item";

        return new PublicCartItemDto(
            item.Id,
            item.MenuItemId,
            itemName,
            item.UnitPrice,
            item.Quantity,
            item.Currency,
            item.GetLineTotal());
    }
}
