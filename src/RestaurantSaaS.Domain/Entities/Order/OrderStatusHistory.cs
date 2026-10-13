
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Orders;

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Order Order { get; private set; } = null!;

    private OrderStatusHistory()
    {
    }

    internal static OrderStatusHistory Create(
        Guid orderId,
        OrderStatus status,
        Guid? changedByUserId,
        string? notes,
        DateTime utcNow)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("ORDER_HISTORY_ORDER_REQUIRED", "Status history requires an order.");

        if (!Enum.IsDefined(status))
            throw new DomainException("ORDER_HISTORY_STATUS_INVALID", "Order status is invalid.");

        if (changedByUserId == Guid.Empty)
            throw new DomainException("ORDER_HISTORY_ACTOR_INVALID", "Changed-by user ID cannot be empty.");

        return new OrderStatusHistory
        {
            OrderId = orderId,
            Status = status,
            ChangedByUserId = changedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes)
                ? null
                : notes.Trim().Length > 1000
                    ? notes.Trim()[..1000]
                    : notes.Trim(),
            CreatedAt = utcNow
        };
    }
}

