using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Carts;

/// <summary>
/// One line inside a cart. UnitPrice is a SERVER-SIDE snapshot taken when the
/// item was added; it is re-verified at checkout (prices may change between
/// add-to-cart and order creation — checkout recomputes from DB, requirement #29).
/// No TenantId: tenancy derives CartItem -> Cart -> Tenant.
/// </summary>
public class CartItem : AuditableEntity
{
    public const int MaxQuantityPerLine = 99;

    public Guid CartId { get; private set; }
    public Guid MenuItemId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Snapshot of server price at add-time. Never client-supplied.</summary>
    public decimal UnitPrice { get; private set; }

    public string Currency { get; private set; } = ValueObjects.Money.DefaultCurrency;

    public Cart Cart { get; private set; } = null!;

    public static CartItem Create(Guid cartId, Guid menuItemId, int quantity, decimal unitPrice, string currency)
    {
        if (cartId == Guid.Empty)
            throw new DomainException("CARTITEM_CART_REQUIRED", "A cart item must belong to a cart.");

        if (menuItemId == Guid.Empty)
            throw new DomainException("CARTITEM_ITEM_REQUIRED", "A cart item must reference a menu item.");

        if (quantity is < 1 or > MaxQuantityPerLine)
            throw new DomainException("CARTITEM_QUANTITY_INVALID",
                $"Quantity must be between 1 and {MaxQuantityPerLine}.");

        if (unitPrice < 0m)
            throw new DomainException("CARTITEM_PRICE_INVALID", "Unit price cannot be negative.");

        return new CartItem
        {
            CartId = cartId,
            MenuItemId = menuItemId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = currency,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void ChangeQuantity(int newQuantity)
    {
        if (newQuantity is < 1 or > MaxQuantityPerLine)
            throw new DomainException("CARTITEM_QUANTITY_INVALID",
                $"Quantity must be between 1 and {MaxQuantityPerLine}.");

        Quantity = newQuantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Refresh the price snapshot during checkout validation.</summary>
    public void RefreshServerPrice(decimal currentPrice, string currency, DateTime utcNow)
    {
        UnitPrice = currentPrice;
        Currency = currency;
        UpdatedAt = utcNow;
    }
}
