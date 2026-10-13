
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Orders;

public class OrderItem : BaseEntity
{
    public const int MaxQuantityPerLine = 999;

    private const decimal MaxMoneyAmount = 99999999999999.9999m;

    public Guid OrderId { get; private set; }
    public Guid? MenuItemId { get; private set; }

    public string ItemName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }
    public string Currency { get; private set; } = ValueObjects.Money.DefaultCurrency;

    public Order Order { get; private set; } = null!;

    private OrderItem()
    {
    }

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

        if (menuItemId == Guid.Empty)
            throw new DomainException("ORDERITEM_MENUITEM_INVALID", "Menu item ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(itemName))
            throw new DomainException("ORDERITEM_NAME_REQUIRED", "Order line requires the item name snapshot.");

        if (itemName.Trim().Length > 200)
            throw new DomainException("ORDERITEM_NAME_TOO_LONG", "Item name cannot exceed 200 characters.");

        if (unitPrice < 0m ||
            unitPrice > MaxMoneyAmount ||
            decimal.Round(unitPrice, 4) != unitPrice)
        {
            throw new DomainException(
                "ORDERITEM_PRICE_INVALID",
                "Order line price must be non-negative and support no more than four decimal places.");
        }

        if (quantity is < 1 or > MaxQuantityPerLine)
            throw new DomainException("ORDERITEM_QUANTITY_INVALID", "Order line quantity is out of range.");

        if (unitPrice != 0m && unitPrice > MaxMoneyAmount / quantity)
            throw new DomainException("ORDERITEM_TOTAL_INVALID", "Order line total exceeds the supported money range.");

        var normalizedCurrency = NormalizeCurrency(currency);

        return new OrderItem
        {
            OrderId = orderId,
            MenuItemId = menuItemId,
            ItemName = itemName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            LineTotal = unitPrice * quantity,
            Currency = normalizedCurrency
        };
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("ORDERITEM_CURRENCY_REQUIRED", "Order line currency is required.");

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || normalized.Any(c => !char.IsAsciiLetter(c)))
            throw new DomainException("ORDERITEM_CURRENCY_INVALID", "Currency must be a three-letter code.");

        return normalized;
    }
}

