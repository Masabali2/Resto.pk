using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Subscriptions;

/// <summary>
/// A tenant's subscription to a plan. History must be preserved: when a tenant
/// changes plans, the old row is Cancelled (not deleted) and a new row starts.
/// Multiple subscriptions per tenant over time (Tenant 1 -> M Subscriptions).
/// </summary>
public class Subscription : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionPlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }

    /// <summary>Nullable for open-ended/"until cancelled" contracts.</summary>
    public DateTime? EndDate { get; private set; }

    public bool AutoRenew { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;
    public SubscriptionPlan Plan { get; private set; } = null!;

    public static Subscription Start(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTime startDate,
        DateTime? endDate,
        bool autoRenew = true)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("SUBSCRIPTION_TENANT_REQUIRED", "Subscription requires a tenant.");

        if (subscriptionPlanId == Guid.Empty)
            throw new DomainException("SUBSCRIPTION_PLAN_REQUIRED", "Subscription requires a plan.");

        if (endDate is { } end && end <= startDate)
            throw new DomainException("SUBSCRIPTION_PERIOD_INVALID", "Subscription end date must be after start date.");

        return new Subscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = subscriptionPlanId,
            StartDate = startDate,
            EndDate = endDate,
            AutoRenew = autoRenew,
            Status = SubscriptionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new DomainException("SUBSCRIPTION_ALREADY_ENDED", "Subscription has already ended.");

        Status = SubscriptionStatus.Cancelled;
        EndDate = utcNow;
        AutoRenew = false;
        UpdatedAt = utcNow;
    }

    public void MarkPastDue(DateTime utcNow)
    {
        if (Status != SubscriptionStatus.Active)
            throw new DomainException("SUBSCRIPTION_NOT_ACTIVE", "Only an active subscription can become past due.");

        Status = SubscriptionStatus.PastDue;
        UpdatedAt = utcNow;
    }

    public void MarkExpired(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled)
            throw new DomainException("SUBSCRIPTION_ALREADY_ENDED", "A cancelled subscription cannot expire again.");

        Status = SubscriptionStatus.Expired;
        EndDate ??= utcNow;
        UpdatedAt = utcNow;
    }

    public void SetAutoRenew(bool autoRenew, DateTime utcNow)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.PastDue))
            throw new DomainException("SUBSCRIPTION_NOT_MODIFIABLE", "Ended subscriptions cannot change renewal settings.");

        AutoRenew = autoRenew;
        UpdatedAt = utcNow;
    }

    /// <summary>Business rule: does this subscription currently entitle the tenant to operate?</summary>
    public bool IsEntitled(DateTime utcNow) =>
        Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.PastDue
        && (EndDate is null || EndDate > utcNow);
}
