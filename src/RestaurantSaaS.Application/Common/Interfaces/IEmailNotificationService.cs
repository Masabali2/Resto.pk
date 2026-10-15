namespace RestaurantSaaS.Application.Common.Interfaces;

using RestaurantSaaS.Domain.Enums;

/// <summary>
/// Centralized email notification orchestration for application events.
///
/// All login and order email alerts flow through this single abstraction so
/// event → recipient mapping, templating, and duplicate prevention live in one
/// place. Implementations enqueue durable outbox messages (never send SMTP
/// inline) so notification failures can never affect the business operation
/// that triggered them.
///
/// The application layer depends only on this interface; SMTP, persistence and
/// background delivery remain Infrastructure concerns.
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Event A: queues a security sign-in email for a successfully
    /// authenticated user. Never throws for delivery reasons; failures are
    /// logged and swallowed so authentication outcomes cannot change.
    /// Deduplicated per session (refresh-token hash) when provided.
    /// </summary>
    Task NotifySuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event B: queues "new order received" emails for the authorized
    /// restaurant recipients (active Owner/Manager staff of the order's
    /// tenant). Call only AFTER the order has been persisted.
    /// </summary>
    Task NotifyNewOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task NotifyPasswordResetAsync(
    Guid userId,
    string resetLink,
    CancellationToken cancellationToken = default);
    Task NotifyOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default);
}
