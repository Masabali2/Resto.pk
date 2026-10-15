
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Notifications;

public class EmailOutboxMessage : AuditableEntity
{
    public const int MaxSubjectLength = 500;
    public const int MaxBodyLength = 32768;
    public const int MaxEventNameLength = 100;
    public const int MaxDedupeKeyLength = 200;
    public const int DefaultMaxAttempts = 5;

    public string RecipientEmail { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string HtmlBody { get; private set; } = null!;

    /// <summary>Stable logical event name, e.g. "auth.signin", "order.created".</summary>
    public string EventName { get; private set; } = null!;

    /// <summary>Stable identifier for the event instance (e.g. token hash or order id:status).</summary>
    public string DedupeKey { get; private set; } = null!;

    public EmailOutboxStatus Status { get; private set; } = EmailOutboxStatus.Pending;
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private EmailOutboxMessage()
    {
    }

    public static EmailOutboxMessage Create(
        string recipientEmail,
        string subject,
        string htmlBody,
        string eventName,
        string dedupeKey,
        int maxAttempts = DefaultMaxAttempts,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new DomainException(
                "EMAIL_OUTBOX_RECIPIENT_REQUIRED",
                "An outbox message requires a recipient email address.");

        if (recipientEmail.Trim().Length > 254)
            throw new DomainException(
                "EMAIL_OUTBOX_RECIPIENT_TOO_LONG",
                "Recipient email cannot exceed 254 characters.");

        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException(
                "EMAIL_OUTBOX_SUBJECT_REQUIRED",
                "An outbox message requires a subject.");

        if (subject.Trim().Length > MaxSubjectLength)
            throw new DomainException(
                "EMAIL_OUTBOX_SUBJECT_TOO_LONG",
                $"Outbox subject cannot exceed {MaxSubjectLength} characters.");

        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new DomainException(
                "EMAIL_OUTBOX_BODY_REQUIRED",
                "An outbox message requires a body.");

        if (htmlBody.Trim().Length > MaxBodyLength)
            throw new DomainException(
                "EMAIL_OUTBOX_BODY_TOO_LONG",
                $"Outbox body cannot exceed {MaxBodyLength} characters.");

        if (string.IsNullOrWhiteSpace(eventName))
            throw new DomainException(
                "EMAIL_OUTBOX_EVENT_REQUIRED",
                "An outbox message requires an event name.");

        if (eventName.Trim().Length > MaxEventNameLength)
            throw new DomainException(
                "EMAIL_OUTBOX_EVENT_TOO_LONG",
                $"Event name cannot exceed {MaxEventNameLength} characters.");

        if (string.IsNullOrWhiteSpace(dedupeKey))
            throw new DomainException(
                "EMAIL_OUTBOX_DEDUPE_KEY_REQUIRED",
                "An outbox message requires a deduplication key.");

        if (dedupeKey.Trim().Length > MaxDedupeKeyLength)
            throw new DomainException(
                "EMAIL_OUTBOX_DEDUPE_KEY_TOO_LONG",
                $"Deduplication key cannot exceed {MaxDedupeKeyLength} characters.");

        if (maxAttempts < 1 || maxAttempts > 100)
            throw new DomainException(
                "EMAIL_OUTBOX_MAX_ATTEMPTS_INVALID",
                "Maximum attempts must be between 1 and 100.");

        return new EmailOutboxMessage
        {
            RecipientEmail = recipientEmail.Trim(),
            Subject = subject.Trim(),
            HtmlBody = htmlBody.Trim(),
            EventName = eventName.Trim(),
            DedupeKey = dedupeKey.Trim(),
            Status = EmailOutboxStatus.Pending,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAtUtc = now,
            CreatedAt = now
        };
    }

    /// <summary>Worker claims the message before attempting SMTP delivery.</summary>
    public void MarkSending(DateTime utcNow)
    {
        if (Status != EmailOutboxStatus.Pending)
            throw new DomainException(
                "EMAIL_OUTBOX_INVALID_CLAIM",
                $"Only pending messages can be claimed. Current status: {Status}.");

        Attempts++;
        Status = EmailOutboxStatus.Sending;
        UpdatedAt = utcNow;
    }

    /// <summary>Only ever called after the email service reports success.</summary>
    public void MarkSent(DateTime utcNow)
    {
        if (Status != EmailOutboxStatus.Sending)
            throw new DomainException(
                "EMAIL_OUTBOX_INVALID_STATE",
                "Only messages currently being sent can be marked as sent.");

        Status = EmailOutboxStatus.Sent;
        SentAtUtc = utcNow;
        LastError = null;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Records a delivery failure. The message returns to Pending for a delayed
    /// retry while attempts remain; otherwise it becomes terminally Failed.
    /// </summary>
    public void MarkFailed(
        string error,
        DateTime utcNow,
        TimeSpan retryDelay)
    {
        if (Status != EmailOutboxStatus.Sending)
            throw new DomainException(
                "EMAIL_OUTBOX_INVALID_STATE",
                "Only messages currently being sent can record a delivery failure.");

        LastError = Truncate(error, 2000);
        UpdatedAt = utcNow;

        if (Attempts >= MaxAttempts)
        {
            Status = EmailOutboxStatus.Failed;
            NextAttemptAtUtc = null;
        }
        else
        {
            Status = EmailOutboxStatus.Pending;
            NextAttemptAtUtc = utcNow + retryDelay;
        }
    }

    public bool IsRetryPending =>
        Status == EmailOutboxStatus.Pending && Attempts > 0;

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;
}
