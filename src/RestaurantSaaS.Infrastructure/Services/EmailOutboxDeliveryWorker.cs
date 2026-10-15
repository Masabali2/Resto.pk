using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Background email outbox delivery worker.
///
/// Polls EmailOutboxMessages and delivers them through the existing
/// IEmailService (SMTP). Delivery semantics:
/// - At-least-once with deduplication: a message is only marked Sent after the
///   email service reports success; a crash mid-send may retry that message
///   (exactly-once SMTP delivery is not claimed).
/// - Concurrent workers cannot process the same row: claiming uses an atomic
///   conditional UPDATE (row must still be Pending when claimed).
/// - Bounded retries with exponential backoff; exhausted messages become
///   terminally Failed and are left for operator inspection.
///
/// The worker creates its own DbContext scopes with tenant filters disabled —
/// it processes platform-wide queue rows, never request-scoped tenant data.
/// </summary>
public sealed class EmailOutboxDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailOutboxDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMinutes(1);
    private const int BatchSize = 20;

    // Stale claims (worker crashed mid-send) are reclaimed after this window.
    private static readonly TimeSpan SendingTimeout = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email outbox delivery worker started.");

        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                await ProcessPendingBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email outbox delivery cycle failed; will retry next poll.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));

        logger.LogInformation("Email outbox delivery worker stopped.");
    }

    private async Task ProcessPendingBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTimeProvider.UtcNow;

        // Reclaim stale in-flight rows from crashed workers so they can retry.
        await dbContext.EmailOutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == EmailOutboxStatus.Sending &&
                        m.UpdatedAt != null &&
                        m.UpdatedAt < now - SendingTimeout)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.Status, EmailOutboxStatus.Pending)
                    .SetProperty(m => m.NextAttemptAtUtc, now),
                cancellationToken);

        var candidateIds = await dbContext.EmailOutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == EmailOutboxStatus.Pending &&
                        (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var id in candidateIds)
        {
            // Atomic claim: only one worker can transition Pending -> Sending.
            var claimed = await dbContext.EmailOutboxMessages
                .IgnoreQueryFilters()
                .Where(m => m.Id == id && m.Status == EmailOutboxStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(m => m.Status, EmailOutboxStatus.Sending)
                        .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                        .SetProperty(m => m.UpdatedAt, now),
                    cancellationToken);

            if (claimed == 0)
            {
                continue; // Another worker already owns this message.
            }

            var message = await dbContext.EmailOutboxMessages
                .IgnoreQueryFilters()
                .FirstAsync(m => m.Id == id, cancellationToken);

            await DeliverAsync(dbContext, emailService, dateTimeProvider, message, cancellationToken);
        }
    }

    private async Task DeliverAsync(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        EmailOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailService.SendAsync(
                message.RecipientEmail,
                message.Subject,
                message.HtmlBody,
                cancellationToken);

            // Never mark successful before the email service reports success.
            message.MarkSent(dateTimeProvider.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Delivered outbox email {MessageId} ({EventName}) to recipient.",
                message.Id,
                message.EventName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Shutdown during delivery of outbox email {MessageId}; row stays in Sending and will be reclaimed.",
                message.Id);
            throw;
        }
        catch (Exception ex)
        {
            var attemptNo = message.Attempts;
            var delay = TimeSpan.FromMinutes(
                Math.Min(BaseRetryDelay.TotalMinutes * Math.Pow(2, attemptNo - 1), 60));

            message.MarkFailed(ex.Message, dateTimeProvider.UtcNow, delay);

            await dbContext.SaveChangesAsync(CancellationToken.None);

            logger.LogWarning(
                ex,
                "Outbox email {MessageId} ({EventName}) delivery attempt {Attempt}/{Max} failed. {State}",
                message.Id,
                message.EventName,
                attemptNo,
                message.MaxAttempts,
                message.Status == EmailOutboxStatus.Failed
                    ? "Retries exhausted; marked Failed."
                    : $"Scheduled retry at {message.NextAttemptAtUtc:O}.");
        }
    }
}
