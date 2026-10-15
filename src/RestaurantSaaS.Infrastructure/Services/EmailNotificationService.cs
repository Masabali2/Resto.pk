
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Centralized email notification orchestration (Infrastructure side).
///
/// Resolves recipients from persisted data only, builds event-specific HTML
/// templates (user-controlled values are HTML-encoded), and enqueues durable
/// EmailOutboxMessage rows. It NEVER sends SMTP inline and NEVER throws for
/// delivery reasons — a notification problem can never fail or alter the
/// authentication or order operation that triggered it.
///
/// Duplicate prevention: every enqueue uses a stable (EventName, DedupeKey)
/// pair backed by a unique index; retries of the same event are ignored.
/// </summary>
public sealed class EmailNotificationService(
    ApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<EmailNotificationService> logger) : IEmailNotificationService
{
    // Stable logical event names used as idempotency namespaces.
    public const string SignInEvent = "auth.signin";
    public const string NewOrderEvent = "order.created";
    public const string OrderStatusEvent = "order.status";

    public async Task NotifySuccessfulLoginAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                logger.LogWarning(
                    "Sign-in notification skipped: no email on file for user {UserId}.",
                    userId);
                return;
            }

            // Dedupe per login session: the refresh-token hash uniquely
            // identifies this sign-in. A retried request reuses the same key,
            // so at most one security email is queued per session.
            var dedupeKey = await GetSessionDedupeKeyAsync(userId, cancellationToken);

            await EnqueueAsync(
                user.Email,
                subject: "New sign-in to your Resto.pk account",
                htmlBody: BuildSignInHtml(user.FullName),
                eventName: SignInEvent,
                dedupeKey: dedupeKey,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Email delivery must never invalidate a successful login.
            logger.LogError(
                ex,
                "Failed to queue sign-in security email for user {UserId}.",
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
                    "New-order notification skipped: order {OrderId} not found.",
                    orderId);
                return;
            }

            var recipients = await ResolveRestaurantRecipientsAsync(
                order.TenantId,
                cancellationToken);

            if (recipients.Count == 0)
            {
                // Documented fallback policy: log and skip. Never fall back to
                // arbitrary platform administrators or the whole platform.
                logger.LogWarning(
                    "New-order notification for order {OrderNumber}: no eligible restaurant " +
                    "recipients configured for tenant {TenantId}. Skipping.",
                    order.OrderNumber,
                    order.TenantId);
                return;
            }

            var html = BuildNewOrderHtml(
                order.Restaurant?.Name ?? "restaurant team",
                order.OrderNumber,
                FormatMoney(order.TotalAmount, order.Currency),
                order.Status.ToString(),
                FormatUtc(order.CreatedAt));

            foreach (var recipient in recipients)
            {
                // Per-recipient dedupe key makes retries idempotent and keeps
                // each authorized recipient to a single message per event.
                await EnqueueAsync(
                    recipient,
                    subject: $"New order received — Order #{order.OrderNumber}",
                    htmlBody: html,
                    eventName: NewOrderEvent,
                    dedupeKey: $"{order.Id}:{recipient}",
                    cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to queue new-order notification emails for order {OrderId}.",
                orderId);
        }
    }

    public async Task NotifyOrderStatusChangedAsync(
        Guid orderId,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        // Only customer-facing statuses defined by the existing state machine
        // produce emails. Ready has no agreed template; New is creation, not a
        // transition. Unchanged/unsupported statuses never notify.
        if (newStatus is not (OrderStatus.Confirmed
                              or OrderStatus.Preparing
                              or OrderStatus.Completed
                              or OrderStatus.Cancelled))
        {
            return;
        }

        try
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .IgnoreQueryFilters() // system path: data comes from the persisted order only
                .Include(o => o.Restaurant)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                logger.LogWarning(
                    "Order status notification skipped: order {OrderId} not found.",
                    orderId);
                return;
            }

            // SECURITY: recipient resolved exclusively from the persisted
            // order relationship — never from caller-supplied input.
            var customerEmail = order.CustomerEmail;

            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                logger.LogInformation(
                    "Order status notification skipped for order {OrderNumber}: " +
                    "no customer email on the persisted order.",
                    order.OrderNumber);
                return;
            }

            var restaurantName = order.Restaurant?.Name ?? "the restaurant";
            var customerName = order.CustomerName;

            var (subject, html) = newStatus switch
            {
                OrderStatus.Confirmed => (
                    $"Your order #{order.OrderNumber} is confirmed",
                    BuildCustomerStatusHtml(
                        customerName,
                        order.OrderNumber,
                        restaurantName,
                        "has been confirmed",
                        newStatus.ToString())),
                OrderStatus.Preparing => (
                    $"Your order #{order.OrderNumber} is being prepared",
                    BuildPreparingHtml(customerName, order.OrderNumber, restaurantName, newStatus.ToString())),
                OrderStatus.Completed => (
                    $"Your order #{order.OrderNumber} is complete",
                    BuildCompletedHtml(customerName, order.OrderNumber, restaurantName)),
                OrderStatus.Cancelled => (
                    $"Your order #{order.OrderNumber} was cancelled",
                    BuildCustomerStatusHtml(
                        customerName,
                        order.OrderNumber,
                        restaurantName,
                        "has been cancelled",
                        newStatus.ToString())),
                _ => default
            };

            if (subject is null)
            {
                return;
            }

            // One email per order per status: retrying the same transition
            // produces the same dedupe key and is ignored by the unique index.
            await EnqueueAsync(
                customerEmail,
                subject,
                html,
                eventName: OrderStatusEvent,
                dedupeKey: $"{order.Id}:{newStatus}",
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to queue order status notification email for order {OrderId} ({Status}).",
                orderId,
                newStatus);
        }
    }

    // ------------------------------------------------------------------
    // Recipient resolution
    // ------------------------------------------------------------------

    /// <summary>
    /// Eligible restaurant recipients: users with an ACTIVE Owner or Manager
    /// staff membership in the order's tenant (the roles that hold
    /// Order.Update / Order.Cancel). Emails are normalized and deduplicated.
    /// Kitchen/Cashier/Waiter staff do not receive order-intake emails.
    /// </summary>
    private async Task<List<string>> ResolveRestaurantRecipientsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var roleNames = new[] { Permissions.TenantRoles.Owner, Permissions.TenantRoles.Manager };

        var emails = await (
            from membership in dbContext.StaffMemberships.IgnoreQueryFilters()
            join role in dbContext.Roles.IgnoreQueryFilters() on membership.RoleId equals role.Id
            join user in dbContext.Users.IgnoreQueryFilters() on membership.UserId equals user.Id
            where membership.TenantId == tenantId
                  && membership.Status == MembershipStatus.Active
                  && role.Name != null
                  && roleNames.Contains(role.Name)
                  && user.Email != null
                  && user.EmailConfirmed
            select user.Email)
            .ToListAsync(cancellationToken);

        return emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string> GetSessionDedupeKeyAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Most recent active session token hash for this user identifies the
        // sign-in event. Falls back to a time-bucketed key if unavailable.
        var latestTokenHash = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => t.TokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(latestTokenHash))
        {
            return latestTokenHash;
        }

        var now = dateTimeProvider.UtcNow;
        return $"novsession:{userId:N}:{now:yyyyMMddHH}";
    }

    // ------------------------------------------------------------------
    // Outbox enqueue (idempotent)
    // ------------------------------------------------------------------

    private async Task EnqueueAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        string eventName,
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.EmailOutboxMessages
            .AnyAsync(
                m => m.EventName == eventName && m.DedupeKey == dedupeKey,
                cancellationToken);

        if (exists)
        {
            // Event already recorded — duplicate suppressed.
            return;
        }

        var message = EmailOutboxMessage.Create(
            recipientEmail,
            subject,
            htmlBody,
            eventName,
            dedupeKey);

        try
        {
            dbContext.EmailOutboxMessages.Add(message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique-index race between concurrent requests/workers: the event
            // was already recorded by someone else. Detach and treat as success.
            if (IsUniqueViolation(ex))
            {
                dbContext.Entry(message).State = EntityState.Detached;
                return;
            }

            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
        && (sqlEx.Number == 2627 || sqlEx.Number == 2601);

    // ------------------------------------------------------------------
    // Templates (all user-controlled values HTML-encoded)
    // ------------------------------------------------------------------

    private static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount:N2}";

    private static string FormatUtc(DateTime utc) =>
        utc.ToString("yyyy-MM-dd HH:mm 'UTC'");

    private static string Enc(string value) =>
        System.Text.Encodings.Web.HtmlEncoder.Default.Encode(value ?? string.Empty);

    private static string Wrap(string heading, params string[] paragraphs)
    {
        var body = string.Join(
            "\n",
            paragraphs.Select(p => $"<p>{p}</p>"));

        return $"""
            <html>
            <body>
                <h2>{heading}</h2>
            {body}
            </body>
            </html>
            """;
    }

    private static string BuildSignInHtml(string userName) => Wrap(
        "New sign-in to your Resto.pk account",
        $"Hello {Enc(userName)},",
        "Your Resto.pk account was successfully signed in.",
        "If you performed this action, no further action is required. " +
        "If you do not recognize this sign-in, please secure your account or contact support.",
        "Regards,<br/>Resto.pk Security Team");

    private static string BuildNewOrderHtml(
        string restaurantName,
        string orderNumber,
        string orderTotal,
        string orderStatus,
        string receivedAt) => Wrap(
        "New order received",
        $"Hello {Enc(restaurantName)},",
        "A new order has been received.",
        $"Order number: {Enc(orderNumber)}<br/>" +
        $"Order total: {Enc(orderTotal)}<br/>" +
        $"Order status: {Enc(orderStatus)}<br/>" +
        $"Received at: {Enc(receivedAt)}",
        "Please open your Resto.pk dashboard to review and process the order.",
        "Regards,<br/>Resto.pk");

    private static string BuildPreparingHtml(
        string customerName,
        string orderNumber,
        string restaurantName,
        string orderStatus) => Wrap(
        "Your order is being prepared",
        $"Hello {Enc(customerName)},",
        $"Your order #{Enc(orderNumber)} is now being prepared by {Enc(restaurantName)}.",
        $"Current status: {Enc(orderStatus)}",
        "You can check the latest order details through Resto.pk.",
        "Regards,<br/>Resto.pk");

    private static string BuildCompletedHtml(
        string customerName,
        string orderNumber,
        string restaurantName) => Wrap(
        "Your order is complete",
        $"Hello {Enc(customerName)},",
        $"Your order #{Enc(orderNumber)} from {Enc(restaurantName)} has been marked as completed.",
        "Thank you for ordering through Resto.pk.",
        "Regards,<br/>Resto.pk");

    private static string BuildCustomerStatusHtml(
        string customerName,
        string orderNumber,
        string restaurantName,
        string statusPhrase,
        string orderStatus) => Wrap(
        $"Order update: #{orderNumber}",
        $"Hello {Enc(customerName)},",
        $"Your order #{Enc(orderNumber)} from {Enc(restaurantName)} {Enc(statusPhrase)}.",
        $"Current status: {Enc(orderStatus)}",
        "You can check the latest order details through Resto.pk.",
        "Regards,<br/>Resto.pk");
}
