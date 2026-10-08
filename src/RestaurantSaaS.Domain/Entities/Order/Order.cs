using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Orders;

/// <summary>
/// A customer order — the most security-critical aggregate in the system.
///
/// SECURITY CONTRACT (requirements #29/#32):
/// - TenantId/RestaurantId are resolved SERVER-SIDE; client values are ignored.
/// - All money fields are computed by the server from current DB prices.
/// - CustomerName/Phone/Email are HISTORICAL SNAPSHOTS: they must survive even
///   if the Customer record is later edited or soft-deleted.
/// - OrderNumber is unique per tenant: UNIQUE(TenantId, OrderNumber).
/// - Status changes go exclusively through <see cref="TransitionTo"/>, which
///   enforces the legal lifecycle graph. Never accept raw status strings.
/// </summary>
public class Order : AuditableEntity, ITenantOwned
{
    /// <summary>Concurrency token: two kitchen screens must not race on one order (requirement #31).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }
    public Guid? CustomerId { get; private set; }

    /// <summary>Human-readable sequential number, unique within the tenant.</summary>
    public string OrderNumber { get; private set; } = null!;

    public OrderStatus Status { get; private set; } = OrderStatus.New;

    // ---- Historical customer snapshot (never re-derived from Customer table) ----
    public string CustomerName { get; private set; } = null!;
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }

    // ---- Server-computed money (decimal 18,4 columns) ----
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = ValueObjects.Money.DefaultCurrency;

    public string? SpecialInstructions { get; private set; }

    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public Restaurant Restaurant { get; private set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

    /// <summary>
    /// Factory used by the checkout use case AFTER the server has loaded menu
    /// items, verified ownership/availability and recomputed all totals.
    /// </summary>
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

        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new DomainException("ORDER_NUMBER_REQUIRED", "Order number is required.");

        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("ORDER_CUSTOMER_NAME_REQUIRED", "Customer name is required for an order.");

        if (subtotal < 0m || discountAmount < 0m || taxAmount < 0m || totalAmount < 0m)
            throw new DomainException("ORDER_MONEY_NEGATIVE", "Order amounts cannot be negative.");

        if (discountAmount > subtotal)
            throw new DomainException("ORDER_DISCOUNT_EXCEEDS_SUBTOTAL", "Discount cannot exceed the subtotal.");

        var expectedTotal = subtotal - discountAmount + taxAmount;
        if (expectedTotal != totalAmount)
            throw new DomainException("ORDER_TOTAL_MISMATCH",
                "Order total does not match subtotal - discount + tax (server-side recalculation failed).");

        return new Order
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            CustomerId = customerId,
            OrderNumber = orderNumber.Trim(),
            Status = OrderStatus.New,
            CustomerName = customerName.Trim(),
            CustomerPhone = customerPhone?.Trim(),
            CustomerEmail = customerEmail?.Trim().ToLowerInvariant(),
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            Currency = currency,
            SpecialInstructions = Truncate(specialInstructions, 2000),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// The ONLY way to change order status. Enforces:
    /// New -> Confirmed -> Preparing -> Ready -> Completed, plus Cancelled from
    /// any non-terminal state. Terminal states (Completed/Cancelled) are frozen.
    /// Returns the created history entry so callers can persist it transactionally.
    /// </summary>
    public OrderStatusHistory TransitionTo(
        OrderStatus newStatus,
        Guid? changedByUserId = null,
        string? notes = null,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (!CanTransitionTo(Status, newStatus))
            throw new DomainException("ORDER_INVALID_TRANSITION",
                $"Cannot transition an order from {Status} to {newStatus}.");

        Status = newStatus;

        if (newStatus == OrderStatus.Completed)
            CompletedAt = now;

        if (newStatus == OrderStatus.Cancelled)
            CancelledAt = now;

        UpdatedAt = now;

        var history = OrderStatusHistory.Create(Id, newStatus, changedByUserId, notes, now);
        StatusHistory.Add(history);
        return history;
    }

    /// <summary>Pure transition rule — unit-testable without EF.</summary>
    public static bool CanTransitionTo(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.New => to is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => to is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => to is OrderStatus.Ready or OrderStatus.Cancelled,
        OrderStatus.Ready => to is OrderStatus.Completed or OrderStatus.Cancelled,
        OrderStatus.Completed => false,   // terminal: a completed order can never become New again
        OrderStatus.Cancelled => false,   // terminal: a cancelled order can never become Preparing
        _ => false
    };

    public bool IsTerminal => Status is OrderStatus.Completed or OrderStatus.Cancelled;

    private static string? Truncate(string? value, int max) =>
        value is { Length: > max } ? value[..max] : value;
}
