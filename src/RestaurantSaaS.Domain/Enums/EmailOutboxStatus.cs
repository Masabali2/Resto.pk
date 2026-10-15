namespace RestaurantSaaS.Domain.Enums;

/// <summary>
/// Delivery lifecycle for queued email outbox messages.
/// Pending -> Sending (claimed by a worker) -> Sent (SMTP success)
/// or back to Pending after a bounded retry, or Failed when retries are exhausted.
/// </summary>
public enum EmailOutboxStatus
{
    /// <summary>Queued, waiting to be picked up by the delivery worker.</summary>
    Pending = 1,

    /// <summary>Claimed by a worker and currently being delivered.</summary>
    Sending = 2,

    /// <summary>The email service reported successful hand-off to SMTP.</summary>
    Sent = 3,

    /// <summary>Bounded retries exhausted or message is permanently undeliverable.</summary>
    Failed = 4
}
