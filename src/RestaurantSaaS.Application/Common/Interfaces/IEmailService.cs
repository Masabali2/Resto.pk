namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>Persistence-agnostic fan-out for real-time events (SignalR in Infrastructure).</summary>
public interface INotificationService
{
    /// <summary>
    /// Publishes to a TENANT-SCOPED group only. Group names are constructed by
    /// the implementation as tenant:{tenantId}[:restaurant:{restaurantId}] —
    /// callers can never broadcast platform-wide.
    /// </summary>
    Task NotifyRestaurantAsync(
        Guid tenantId,
        Guid restaurantId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);

    Task NotifyTenantAsync(Guid tenantId, string eventName, object payload, CancellationToken cancellationToken = default);
}
