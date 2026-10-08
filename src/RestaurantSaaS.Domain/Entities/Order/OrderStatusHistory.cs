using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Domain.Entities.Orders;

/// <summary>
/// Append-only trail of every order status change (who/when/why).
/// Created exclusively via <see cref="Order.TransitionTo"/> so the history can
/// never diverge from the actual status sequence.
/// </summary>
public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; private set; }
    public OrderStatus Status { get; private set; }

    /// <summary>Staff user who made the change; null for customer/system actions.</summary>
    public Guid? ChangedByUserId { get; private set; }

    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Order Order { get; private set; } = null!;

    public static OrderStatusHistory Create(
        Guid orderId,
        OrderStatus status,
        Guid? changedByUserId,
        string? notes,
        DateTime utcNow) => new()
        {
            OrderId = orderId,
            Status = status,
            ChangedByUserId = changedByUserId,
            Notes = notes is { Length: > 1000 } ? notes[..1000] : notes,
            CreatedAt = utcNow
        };
}
