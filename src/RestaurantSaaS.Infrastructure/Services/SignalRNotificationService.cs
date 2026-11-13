using Microsoft.AspNetCore.SignalR;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Infrastructure.Hubs;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// SignalR-backed implementation of <see cref="INotificationService"/>.
///
/// Fan-out is delivered to tenant-scoped SignalR groups so only authorized
/// staff of the relevant tenant/restaurant receive order events. Tracking
/// tokens are never embedded in group names.
/// </summary>
public sealed class SignalRNotificationService(
    IHubContext<OrderHub> hubContext) : INotificationService
{
    public Task NotifyRestaurantAsync(
        Guid tenantId,
        Guid restaurantId,
        string eventName,
        object payload,
        CancellationToken ct = default)
    {
        var group = GroupNames.Restaurant(tenantId, restaurantId);
        return hubContext.Clients.Group(group).SendAsync(eventName, new[] { payload }, ct);
    }

    public Task NotifyTenantAsync(
        Guid tenantId,
        string eventName,
        object payload,
        CancellationToken ct = default)
    {
        var group = $"tenant:{tenantId}";
        return hubContext.Clients.Group(group).SendAsync(eventName, new[] { payload }, ct);
    }
}
