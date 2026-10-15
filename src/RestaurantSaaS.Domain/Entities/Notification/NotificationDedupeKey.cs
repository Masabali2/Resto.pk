using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Notifications;

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
