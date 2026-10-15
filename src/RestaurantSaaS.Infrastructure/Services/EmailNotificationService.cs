using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class EmailNotificationService(
    ApplicationDbContext dbContext,
    IEmailService emailService,
    ILogger<EmailNotificationService> logger) : IEmailNotificationService
{
    public async Task NotifyLoginAsync(
        Guid userId,
        string email,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var dedupeKey = $"login:{userId}:{DateTime.UtcNow:yyyyMMddHH}";

        if (await IsDuplicateAsync(dedupeKey, cancellationToken))
        {
            return;
        }

        var (subject, body) = EmailNotificationTemplates.LoginAlert(userName);

        await TrySendAsync(email, subject, body, dedupeKey, cancellationToken);
    }

    public async Task NotifyNewOrderReceivedAsync(
        Guid tenantId,
        Guid restaurantId,
        Guid orderId,
        string orderNumber,
        decimal totalAmount,
        string currency,
        string status,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var dedupeKey = $"new-order:{orderId}";

        if (await IsDuplicateAsync(dedupeKey, cancellationToken))
        {
            return;
        }

        var restaurantName = await dbContext.Restaurants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Id == restaurantId && r.TenantId == tenantId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (restaurantName is null)
        {
            logger.LogWarning(
                "New-order notification skipped: restaurant {RestaurantId} not found for tenant {TenantId}.",
                restaurantId,
                tenantId);
            return;
        }

        var recipients = await ResolveRestaurantRecipientsAsync(tenantId, cancellationToken);

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "New-order notification has no configured Owner/Manager recipients for tenant {TenantId}, order {OrderId}. No fallback broadcast applied.",
                tenantId,
                orderId);
            return;
        }

        var (subject, body) = EmailNotificationTemplates.NewOrder(
            restaurantName, orderNumber, totalAmount, currency, status, receivedAtUtc);

        foreach (var recipient in recipients)
        {
            RecordDedupe(tenantId, recipient.UserId, subject, $"{dedupeKey}:{recipient.UserId}");
            await TrySendAsync(recipient.Email, subject, body, $"{dedupeKey}:{recipient.UserId}", cancellationToken);
        }

        await SafeSaveChangesAsync(cancellationToken);
    }

    public async Task NotifyOrderStatusChangedAsync(
        string customerEmail,
        string customerName,
        string orderNumber,
        string restaurantName,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return;
        }

        var template = EmailNotificationTemplates.CustomerStatusUpdate(
            customerName, orderNumber, restaurantName, status);

        if (template is null)
        {
            return;
        }

        var (subject, body) = template.Value;

        await TrySendAsync(customerEmail, subject, body, $"status-email:{orderNumber}:{status}", cancellationToken);
    }

    private async Task<List<(Guid UserId, string Email)>> ResolveRestaurantRecipientsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var roleIds = await dbContext.Roles
            .Where(r => r.Name == TenantRoles.Owner || r.Name == TenantRoles.Manager)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return [];
        }

        var members = await dbContext.StaffMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId &&
                m.Status == MembershipStatus.Active &&
                roleIds.Contains(m.RoleId))
            .Join(
                dbContext.Users.IgnoreQueryFilters(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        return members
            .Where(m => !string.IsNullOrWhiteSpace(m.Email))
            .GroupBy(m => m.Email!.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .Select(m => (m.Id, m.Email!))
            .ToList();
    }

    private async Task<bool> IsDuplicateAsync(string dedupeKey, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Set<Notification>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    n => n.RelatedEntityType == "Dedupe" && n.Message == dedupeKey,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Duplicate check failed for key {Key}.", dedupeKey);
            return false;
        }
    }

    private void RecordDedupe(Guid tenantId, Guid userId, string title, string dedupeKey)
    {
        var marker = Notification.Create(
            tenantId,
            userId,
            NotificationType.System,
            Truncate(title, 200),
            Truncate(dedupeKey, 4000),
            "Dedupe",
            null);

        dbContext.Set<Notification>().Add(marker);
    }

    private async Task SafeSaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Notification delivery-state save failed.");
        }
    }

    private async Task TrySendAsync(
        string to,
        string subject,
        string body,
        string correlationKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailService.SendAsync(to, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Email delivery failed for {CorrelationKey}. Authentication and business state are unaffected.",
                correlationKey);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
