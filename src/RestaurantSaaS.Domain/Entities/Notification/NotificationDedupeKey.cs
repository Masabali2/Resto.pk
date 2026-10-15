using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Notifications;

/// <summary>
/// Durable in-app notification dedupe record. One row per logical event key
/// (e.g. "auth.signin:{userId}:{sessionHash}" or
/// "order.created:{orderId}:{recipientUserId}"). Producers insert through the
/// unique index before writing the user-facing Notification row, so retried
/// events can never create duplicate alerts for a recipient.
///
/// Deliberately NOT tenant-owned: sign-in security alerts have no guaranteed
/// tenant context and the table stores no personal data — only the stable
/// event key.
/// </summary>
public class NotificationDedupeKey : AuditableEntity
{
    public const int MaxKeyLength = 300;

    public string EventKey { get; private set; } = null!;

    private NotificationDedupeKey()
    {
    }

    public static NotificationDedupeKey Create(
        string eventKey,
        DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            throw new DomainException(
                "NOTIFICATION_DEDUPE_KEY_REQUIRED",
                "A dedupe record requires an event key.");

        var trimmed = eventKey.Trim();

        if (trimmed.Length > MaxKeyLength)
            throw new DomainException(
                "NOTIFICATION_DEDUPE_KEY_TOO_LONG",
                $"Event key cannot exceed {MaxKeyLength} characters.");

        return new NotificationDedupeKey
        {
            EventKey = trimmed,
            CreatedAt = nowUtc ?? DateTime.UtcNow
        };
    }
}
