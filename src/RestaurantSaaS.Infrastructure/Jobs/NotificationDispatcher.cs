using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Jobs;

public sealed class NotificationDispatcher(IServiceScopeFactory scopeFactory)
    : INotificationDispatcher
{
    public void DispatchLogin(Guid userId, string email, string userName) =>
        FireAndForget(service => service.NotifyLoginAsync(userId, email, userName));

    public void DispatchNewOrder(
        Guid tenantId,
        Guid restaurantId,
        Guid orderId,
        string orderNumber,
        decimal totalAmount,
        string currency,
        string status,
        DateTime receivedAtUtc) =>
        FireAndForget(service => service.NotifyNewOrderReceivedAsync(
            tenantId,
            restaurantId,
            orderId,
            orderNumber,
            totalAmount,
            currency,
            status,
            receivedAtUtc));

    public void DispatchOrderStatusChanged(
        string customerEmail,
        string customerName,
        string orderNumber,
        string restaurantName,
        string status) =>
        FireAndForget(service => service.NotifyOrderStatusChangedAsync(
            customerEmail,
            customerName,
            orderNumber,
            restaurantName,
            status));

    private void FireAndForget(Func<IEmailNotificationService, Task> operation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IEmailNotificationService>();
                await operation(service);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Notification dispatch failed: {exception.Message}");
            }
        });
    }
}
