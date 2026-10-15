namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IEmailNotificationService
{
    Task NotifyLoginAsync(
        Guid userId,
        string email,
        string userName,
        CancellationToken cancellationToken = default);

    Task NotifyNewOrderReceivedAsync(
        Guid tenantId,
        Guid restaurantId,
        Guid orderId,
        string orderNumber,
        decimal totalAmount,
        string currency,
        string status,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task NotifyOrderStatusChangedAsync(
        string customerEmail,
        string customerName,
        string orderNumber,
        string restaurantName,
        string status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fire-and-dispatch abstraction so request paths never await SMTP delivery.
/// Implementations must swallow and log dispatch failures; notification
/// delivery can never fail the triggering business operation.
/// </summary>
public interface INotificationDispatcher
{
    void DispatchLogin(Guid userId, string email, string userName);

    void DispatchNewOrder(
        Guid tenantId,
        Guid restaurantId,
        Guid orderId,
        string orderNumber,
        decimal totalAmount,
        string currency,
        string status,
        DateTime receivedAtUtc);

    void DispatchOrderStatusChanged(
        string customerEmail,
        string customerName,
        string orderNumber,
        string restaurantName,
        string status);
}
