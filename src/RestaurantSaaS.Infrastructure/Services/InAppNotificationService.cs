using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// In-app notification orchestration (Infrastructure side).
///
/// Writes persistent Notification rows (existing entity/table) so recipients
/// see alerts inside the Resto.pk application. Current consumers:
///  - Successful sign-in  -> in-app security alert for the authenticated user.
///  - New order persisted -> in-app "new order received" alert for the
///    restaurant owner(s) of the order's tenant (application alert, not email).
///
/// Recipients are resolved exclusively from persisted data. The service never
/// throws for delivery reasons and never mutates orders, users, or tenants.
///
/// Duplicate prevention: every event carries a stable key inserted through the
/// unique index on NotificationDedupeKeys BEFORE the user-facing row is
/// written. Retried or concurrent events collide on the index and are ignored,
/// so one logical event creates at most one notification per recipient.
/// </summary>
public sealed class InAppNotificationService(
    ApplicationDbContext dbContext,
    ILogger<InAppNotificationService> logger) : IInAppNotificationService
{
    // Stable logical event namespaces used for duplicate prevention.
    public const string SignInEvent = "auth.signin";
    public const string NewOrderEvent = "order.created";

    public async Task NotifySuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user is null)
            {
                logger.LogWarning(
                    "In-app sign-in notification skipped: user {UserId} not found.",
                    userId);
                return;
            }

            // Notification rows require a tenant; attach to the user's first
            // active tenant membership when one exists, otherwise skip
            // (never guess a tenant — no cross-tenant leakage).
            var tenantId = await ResolveUserTenantAsync(userId, cancellationToken);

            if (tenantId is null)
            {
                logger.LogInformation(
                    "In-app sign-in notification skipped for user {UserId}: " +
                    "no active tenant membership.",
                    userId);
                return;
            }

            var sessionKey = await GetSessionDedupeKeyAsync(userId, cancellationToken);

            var recorded = await TryRecordEventAsync(
                $"{SignInEvent}:{userId}:{sessionKey}",
                cancellationToken);

            if (!recorded)
            {
                return; // This exact sign-in was already notified.
            }

            AddNotification(
                tenantId.Value,
                userId,
                NotificationType.SecuritySignIn,
                title: "New sign-in to your Resto.pk account",
                message:
                    $"Hello {user.FullName}, your Resto.pk account was successfully " +
                    $"signed in. If you do not recognize this sign-in, please secure " +
                    $"your account or contact support.",
                relatedEntityType: "User",
                relatedEntityId: userId);

            await SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Delivery problems must never affect a completed login.
            logger.LogError(
                ex,
                "Failed to record in-app sign-in notification for user {UserId}.",
                userId);
        }
    }

    public async Task NotifyNewOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .IgnoreQueryFilters() // system path: tenant taken from the persisted order itself
                .Include(o => o.Restaurant)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                logger.LogWarning(
                    "In-app new-order notification skipped: order {OrderId} not found.",
                    orderId);
                return;
            }

            var recipients = await ResolveRestaurantOwnerIdsAsync(
                order.TenantId,
                cancellationToken);

            if (recipients.Count == 0)
            {
                // Documented fallback policy: log and skip. Never notify
                // arbitrary platform administrators or other tenants.
                logger.LogWarning(
                    "In-app new-order notification for order {OrderNumber}: no eligible " +
                    "restaurant owner recipients configured for tenant {TenantId}. Skipping.",
                    order.OrderNumber,
                    order.TenantId);
                return;
            }

            var restaurantName = order.Restaurant?.Name ?? "the restaurant";
            var title = $"New order received — Order #{order.OrderNumber}";
            var message =
                $"A new order has been received at {restaurantName}. " +
                $"Order number: {order.OrderNumber} • " +
                $"Total: {order.TotalAmount:0.00} {order.Currency} • " +
                $"Status: {order.Status}. " +
                "Open your Resto.pk dashboard to review and process the order.";

            var anyWritten = false;

            foreach (var recipientUserId in recipients)
            {
                // Per-recipient stable key: retries of the same order event
                // notify each owner exactly once.
                var recorded = await TryRecordEventAsync(
                    $"{NewOrderEvent}:{order.Id}:{recipientUserId}",
                    cancellationToken);

                if (!recorded)
                {
                    continue;
                }

                AddNotification(
                    order.TenantId,
                    recipientUserId,
                    NotificationType.NewOrder,
                    title,
                    message,
                    relatedEntityType: "Order",
                    relatedEntityId: order.Id);

                anyWritten = true;
            }

            if (anyWritten)
            {
                await SaveAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Delivery problems must never fail the order workflow.
            logger.LogError(
                ex,
                "Failed to record in-app new-order notifications for order {OrderId}.",
                orderId);
        }
    }

    // ------------------------------------------------------------------
    // Recipient resolution (persisted data only)
    // ------------------------------------------------------------------

    /// <summary>
    /// Eligible restaurant recipients for the in-app new-order alert: users
    /// with an ACTIVE staff membership whose role name is Owner in the
    /// order's tenant (exact match — Kitchen/Cashier/Waiter staff excluded).
    /// Deduplicated by user id.
    /// </summary>
    private async Task<List<Guid>> ResolveRestaurantOwnerIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var ownerRoleId = await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => r.Name == Permissions.TenantRoles.Owner)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerRoleId == Guid.Empty)
        {
            return [];
        }

        return await dbContext.StaffMemberships
            .IgnoreQueryFilters()
            .Where(m =>
                m.TenantId == tenantId &&
                m.RoleId == ownerRoleId &&
                m.Status == MembershipStatus.Active)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveUserTenantAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.StaffMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.JoinedAt)
            .Select(m => (Guid?)m.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GetSessionDedupeKeyAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Most recent active session token hash identifies this sign-in,
        // mirroring the email outbox dedupe strategy.
        var latestTokenHash = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => t.TokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(latestTokenHash))
        {
            return DateTime.UtcNow.ToString("yyyyMMddHH");
        }

        return latestTokenHash.Length > 64
            ? latestTokenHash[..64]
            : latestTokenHash;
    }

    // ------------------------------------------------------------------
    // Persistence helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Inserts the stable event key through the unique index. Returns false
    /// when the event was already recorded (duplicate suppressed), including
    /// races with concurrent requests/workers.
    /// </summary>
    private async Task<bool> TryRecordEventAsync(
        string eventKey,
        CancellationToken cancellationToken)
    {
        var key = NotificationDedupeKey.Create(eventKey);

        try
        {
            dbContext.NotificationDedupeKeys.Add(key);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            dbContext.Entry(key).State = EntityState.Detached;
            return false;
        }
    }

    private void AddNotification(
        Guid tenantId,
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string relatedEntityType,
        Guid relatedEntityId)
    {
        dbContext.Notifications.Add(Notification.Create(
            tenantId,
            userId,
            type,
            title,
            message,
            relatedEntityType,
            relatedEntityId));
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent duplicate race after key insert — nothing further
            // to do; the winning request already wrote the notification.
            logger.LogDebug(
                ex,
                "In-app notification save collided with a concurrent duplicate; suppressed.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
        (sql.Number is 2601 or 2627);
}
