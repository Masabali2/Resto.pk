
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Orders;

public class Order : AuditableEntity, ITenantOwned
{
    private const decimal MaxMoneyAmount = 99999999999999.9999m;

    public byte[] RowVersion { get; private set; } = [];

    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }
    public Guid? CustomerId { get; private set; }

    public string OrderNumber { get; private set; } = null!;
    public OrderStatus Status { get; private set; } = OrderStatus.New;

    public string CustomerName { get; private set; } = null!;
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = ValueObjects.Money.DefaultCurrency;

    public string? SpecialInstructions { get; private set; }

    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public Restaurant Restaurant { get; private set; } = null!;

    public ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; private set; } = new List<OrderStatusHistory>();

    private Order()
    {
    }

    public static Order Create(
        Guid tenantId,
        Guid restaurantId,
        Guid? customerId,
        string orderNumber,
        string customerName,
        string? customerPhone,
        string? customerEmail,
        decimal subtotal,
        decimal discountAmount,
        decimal taxAmount,
        decimal totalAmount,
        string currency,
        string? specialInstructions)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("ORDER_TENANT_REQUIRED", "An order must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException("ORDER_RESTAURANT_REQUIRED", "An order must belong to a restaurant.");

        if (customerId == Guid.Empty)
            throw new DomainException("ORDER_CUSTOMER_INVALID", "Customer ID cannot be empty.");

        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new DomainException("ORDER_NUMBER_REQUIRED", "Order number is required.");

        if (orderNumber.Trim().Length > 50)
            throw new DomainException("ORDER_NUMBER_TOO_LONG", "Order number cannot exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("ORDER_CUSTOMER_NAME_REQUIRED", "Customer name is required.");

        if (customerName.Trim().Length > 150)
            throw new DomainException("ORDER_CUSTOMER_NAME_TOO_LONG", "Customer name cannot exceed 150 characters.");

        if (customerPhone?.Trim().Length > 30)
            throw new DomainException("ORDER_CUSTOMER_PHONE_TOO_LONG", "Customer phone cannot exceed 30 characters.");

        if (customerEmail?.Trim().Length > 254)
            throw new DomainException("ORDER_CUSTOMER_EMAIL_TOO_LONG", "Customer email cannot exceed 254 characters.");

        ValidateMoney(subtotal, nameof(subtotal));
        ValidateMoney(discountAmount, nameof(discountAmount));
        ValidateMoney(taxAmount, nameof(taxAmount));
        ValidateMoney(totalAmount, nameof(totalAmount));

        if (discountAmount > subtotal)
            throw new DomainException("ORDER_DISCOUNT_EXCEEDS_SUBTOTAL", "Discount cannot exceed the subtotal.");

        if (subtotal - discountAmount > MaxMoneyAmount - taxAmount)
            throw new DomainException("ORDER_TOTAL_OUT_OF_RANGE", "Calculated order total exceeds the supported money range.");

        var expectedTotal = subtotal - discountAmount + taxAmount;

        if (expectedTotal != totalAmount)
            throw new DomainException(
                "ORDER_TOTAL_MISMATCH",
                "Order total must equal subtotal minus discount plus tax.");

        var normalizedCurrency = NormalizeCurrency(currency);
        var normalizedEmail = NormalizeOptional(customerEmail);

        return new Order
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            CustomerId = customerId,
            OrderNumber = orderNumber.Trim(),
            Status = OrderStatus.New,
            CustomerName = customerName.Trim(),
            CustomerPhone = NormalizeOptional(customerPhone),
            CustomerEmail = normalizedEmail?.ToLowerInvariant(),
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            Currency = normalizedCurrency,
            SpecialInstructions = Truncate(NormalizeOptional(specialInstructions), 2000),
            CreatedAt = DateTime.UtcNow
        };
    }

    public OrderStatusHistory TransitionTo(
        OrderStatus newStatus,
        Guid? changedByUserId = null,
        string? notes = null,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (changedByUserId == Guid.Empty)
            throw new DomainException("ORDER_ACTOR_INVALID", "Changed-by user ID cannot be empty.");

        if (!CanTransitionTo(Status, newStatus))
            throw new DomainException(
                "ORDER_INVALID_TRANSITION",
                $"Cannot transition an order from {Status} to {newStatus}.");

        var history = OrderStatusHistory.Create(
            Id,
            newStatus,
            changedByUserId,
            notes,
            now);

        Status = newStatus;

        if (newStatus == OrderStatus.Completed)
            CompletedAt = now;

        if (newStatus == OrderStatus.Cancelled)
            CancelledAt = now;

        UpdatedAt = now;
        StatusHistory.Add(history);

        return history;
    }

    public static bool CanTransitionTo(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.New => to is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => to is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => to is OrderStatus.Ready or OrderStatus.Cancelled,
        OrderStatus.Ready => to is OrderStatus.Completed or OrderStatus.Cancelled,
        OrderStatus.Completed => false,
        OrderStatus.Cancelled => false,
        _ => false
    };

    public bool IsTerminal => Status is OrderStatus.Completed or OrderStatus.Cancelled;

    private static void ValidateMoney(decimal amount, string fieldName)
    {
        if (amount < 0m || amount > MaxMoneyAmount || decimal.Round(amount, 4) != amount)
            throw new DomainException(
                "ORDER_MONEY_INVALID",
                $"{fieldName} must be non-negative and support no more than four decimal places.");
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("ORDER_CURRENCY_REQUIRED", "Currency is required.");

        var normalized = currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || normalized.Any(c => !char.IsAsciiLetter(c)))
            throw new DomainException("ORDER_CURRENCY_INVALID", "Currency must be a three-letter code.");

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int max) =>
        value is not null && value.Length > max ? value[..max] : value;
}
