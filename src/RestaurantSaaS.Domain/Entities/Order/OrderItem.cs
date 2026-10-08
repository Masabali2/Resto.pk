using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Orders;

/// <summary>
/// Immutable historical line of an order.
///
/// SNAPSHOT RULE (requirement #5): ItemName/UnitPrice are copied at checkout.
/// MenuItemId is nullable because menu items may be soft/purged later — but
/// historical totals must NEVER be recomputed from live MenuItem prices.
/// No TenantId: tenancy derives OrderItem -> Order -> Tenant.
/// </summary>
public class OrderItem : BaseEntity
{
    public const int MaxQuantityPerLine = 999;

    public Guid OrderId { get; private set; }

    /// <summary>Nullable on purpose: the referenced item may no longer exist.</summary>
    public Guid? MenuItemId { get; private set; }

    /// <summary>Name at time of ordering.</summary>
    public string ItemName { get; private set; } = null!;

    /// <summary>Server price at time of ordering.</summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Computed line total (UnitPrice * Quantity), stored for audit stability.</summary>
    public decimal LineTotal { get; private set; }

    public string Currency { get; private set; } = ValueObjects.Money.DefaultCurrency;

    public Order Order { get; private set; } = null!;

    public static OrderItem CreateSnapshot(
        Guid orderId,
        Guid? menuItemId,
        string itemName,
        decimal unitPrice,
        int quantity,
        string currency)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("ORDERITEM_ORDER_REQUIRED", "An order line must belong to an order.");

        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("ORDERITEM_NAME_REQUIRED", "Order line requires the item name snapshot.");

        if (unitPrice < 0m)
            throw new DomainException("ORDERITEM_PRICE_INVALID", "Order line price cannot be negative.");

        if (quantity is < 1 or > MaxQuantityPerLine)
            throw new DomainException("ORDERITEM_QUANTITY_INVALID", "Order line quantity is out of range.");

        return new OrderItem
        {
            OrderId = orderId,
            MenuItemId = menuItemId,
            ItemName = itemName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            LineTotal = unitPrice * quantity,
            Currency = currency
        };
    }
}
