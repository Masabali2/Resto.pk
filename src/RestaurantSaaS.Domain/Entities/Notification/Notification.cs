
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Notifications;

public class Notification : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; private set; }

    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;

    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification()
    {
    }

    public static Notification Create(
        Guid tenantId,
        Guid? userId,
        NotificationType type,
        string title,
        string message,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException(
                "NOTIFICATION_TENANT_REQUIRED",
                "A notification must belong to a tenant.");

        if (userId == Guid.Empty)
            throw new DomainException(
                "NOTIFICATION_USER_INVALID",
                "User ID cannot be empty.");

        if (!Enum.IsDefined(type))
            throw new DomainException(
                "NOTIFICATION_TYPE_INVALID",
                "Notification type is invalid.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException(
                "NOTIFICATION_TITLE_REQUIRED",
                "Notification title is required.");

        if (title.Trim().Length > 200)
            throw new DomainException(
                "NOTIFICATION_TITLE_TOO_LONG",
                "Notification title cannot exceed 200 characters.");

        if (string.IsNullOrWhiteSpace(message))
            throw new DomainException(
                "NOTIFICATION_MESSAGE_REQUIRED",
                "Notification message is required.");

        if (message.Trim().Length > 4000)
            throw new DomainException(
                "NOTIFICATION_MESSAGE_TOO_LONG",
                "Notification message cannot exceed 4000 characters.");

        if (relatedEntityId == Guid.Empty)
            throw new DomainException(
                "NOTIFICATION_RELATED_ENTITY_INVALID",
                "Related entity ID cannot be empty.");

        var normalizedEntityType = string.IsNullOrWhiteSpace(relatedEntityType)
            ? null
            : relatedEntityType.Trim();

        if (normalizedEntityType?.Length > 100)
            throw new DomainException(
                "NOTIFICATION_RELATED_ENTITY_TYPE_TOO_LONG",
                "Related entity type cannot exceed 100 characters.");

        if ((normalizedEntityType is null) != (relatedEntityId is null))
            throw new DomainException(
                "NOTIFICATION_RELATED_ENTITY_INCOMPLETE",
                "Related entity type and ID must either both be provided or both be omitted.");

        return new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            RelatedEntityType = normalizedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRead(DateTime utcNow)
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAt = utcNow;
    }

    public void MarkUnread()
    {
        if (!IsRead)
            return;

        IsRead = false;
        ReadAt = null;
    }
}

