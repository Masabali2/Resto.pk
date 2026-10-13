
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Carts;

public class CartItem : AuditableEntity
{
    public const int MaxQuantityPerLine = 99;
    public const decimal MaxUnitPrice = 9999999999999999.99m;

    public Guid CartId { get; private set; }

    public Guid MenuItemId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public Cart Cart { get; private set; } = null!;

    private CartItem()
    {
    }

    public static CartItem Create(
        Guid cartId,
        Guid menuItemId,
        int quantity,
        decimal unitPrice,
        string currency)
    {
        if (cartId == Guid.Empty)
        {
            throw new DomainException(
                "CARTITEM_CART_REQUIRED",
                "A cart item must belong to a cart.");
        }

        if (menuItemId == Guid.Empty)
        {
            throw new DomainException(
                "CARTITEM_ITEM_REQUIRED",
                "A cart item must reference a menu item.");
        }

        ValidateQuantity(quantity);
        ValidatePrice(unitPrice);

        var normalizedCurrency = NormalizeCurrency(currency);

        return new CartItem
        {
            CartId = cartId,
            MenuItemId = menuItemId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = normalizedCurrency,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void ChangeQuantity(int newQuantity, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        ValidateQuantity(newQuantity);

        Quantity = newQuantity;
        UpdatedAt = utcNow;
    }

    public void RefreshServerPrice(
        decimal currentPrice,
        string currency,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        ValidatePrice(currentPrice);

        UnitPrice = currentPrice;
        Currency = NormalizeCurrency(currency);
        UpdatedAt = utcNow;
    }

    public decimal GetLineTotal() => UnitPrice * Quantity;

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > MaxQuantityPerLine)
        {
            throw new DomainException(
                "CARTITEM_QUANTITY_INVALID",
                $"Quantity must be between 1 and {MaxQuantityPerLine}.");
        }
    }

    private static void ValidatePrice(decimal price)
    {
        if (price < 0m || price > MaxUnitPrice ||
            decimal.Round(price, 2) != price)
        {
            throw new DomainException(
                "CARTITEM_PRICE_INVALID",
                "Unit price must be non-negative, within the supported range, and have no more than two decimal places.");
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException(
                "CARTITEM_CURRENCY_REQUIRED",
                "Currency is required.");
        }

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 ||
            normalized.Any(c => c is < 'A' or > 'Z'))
        {
            throw new DomainException(
                "CARTITEM_CURRENCY_INVALID",
                "Currency must be a three-letter code.");
        }

        return normalized;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(
                "CARTITEM_TIME_INVALID",
                "The supplied time must be UTC.");
        }
    }
}

