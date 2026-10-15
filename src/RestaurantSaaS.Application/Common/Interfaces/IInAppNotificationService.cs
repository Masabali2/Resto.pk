namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Centralized in-app notification orchestration for application events.
///
/// Complements <see cref="IEmailNotificationService"/>: email alerts are
/// delivered through the SMTP outbox, while in-app notifications are stored
/// as persistent <c>Notification</c> rows that the authenticated recipient
/// sees inside the Resto.pk application (e.g. the owner receives an in-app
/// alert when a new order arrives — no email for that event).
///
/// Implementations must never throw for delivery reasons and must never
/// alter the business operation that triggered the notification. Duplicate
/// suppression is enforced with stable dedupe keys so retried events create
/// at most one notification record per recipient.
/// </summary>
public interface IInAppNotificationService
{
    /// <summary>
    /// Records an in-app security sign-in notification for the user whose
    /// login just succeeded and persisted. Call only AFTER the login workflow
    /// has completed successfully; failures are logged and swallowed.
    /// Deduplicated per sign-in session.
    /// </summary>
    Task NotifySuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records in-app "new order received" notifications for the authorized
    /// restaurant recipients (active Owner staff of the order's tenant) so
    /// they receive the alert inside the application. Call only AFTER the
    /// order has been persisted.
    /// </summary>
    Task NotifyNewOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
