using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Domain.Entities.Notifications;

/// <summary>
/// In-app notification. Tenant-scoped; optionally targeted at one user
/// (UserId null => tenant-wide broadcast to staff).
/// The polymorphic RelatedEntityType/RelatedEntityId pair is accepted for V1:
/// notifications are advisory UI pointers, not relational integrity anchors.
/// </summary>
public class Notification : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; private set; }

    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;

    /// <summary>e.g. "Order", "Subscription". Free-form by design (V1).</summary>
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public static Notification Create(
        Guid tenantId,
        Guid? userId,
        NotificationType type,
        string title,
        string message,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        return new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRead(DateTime utcNow)
    {
        if (IsRead) return; // idempotent
        IsRead = true;
        ReadAt = utcNow;
    }
}
