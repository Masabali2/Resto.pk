
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Domain.Entities.Subscriptions;

public class Subscription : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionPlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }

    public bool AutoRenew { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;
    public SubscriptionPlan Plan { get; private set; } = null!;

    private Subscription()
    {
    }

    public static Subscription Start(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTime startDate,
        DateTime? endDate,
        bool autoRenew = true)
    {
        return Create(
            tenantId,
            subscriptionPlanId,
            startDate,
            endDate,
            SubscriptionStatus.Active,
            autoRenew);
    }

    public static Subscription StartTrial(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTime startDate,
        DateTime endDate)
    {
        return Create(
            tenantId,
            subscriptionPlanId,
            startDate,
            endDate,
            SubscriptionStatus.Trial,
            autoRenew: false);
    }

    private static Subscription Create(
        Guid tenantId,
        Guid subscriptionPlanId,
        DateTime startDate,
        DateTime? endDate,
        SubscriptionStatus status,
        bool autoRenew)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException(
                "SUBSCRIPTION_TENANT_REQUIRED",
                "Subscription requires a tenant.");

        if (subscriptionPlanId == Guid.Empty)
            throw new DomainException(
                "SUBSCRIPTION_PLAN_REQUIRED",
                "Subscription requires a plan.");

        if (endDate.HasValue && endDate.Value <= startDate)
            throw new DomainException(
                "SUBSCRIPTION_PERIOD_INVALID",
                "Subscription end date must be after start date.");

        var now = DateTime.UtcNow;

        return new Subscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = subscriptionPlanId,
            StartDate = startDate,
            EndDate = endDate,
            AutoRenew = autoRenew,
            Status = status,
            CreatedAt = now
        };
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new DomainException(
                "SUBSCRIPTION_ALREADY_ENDED",
                "Subscription has already ended.");

        Status = SubscriptionStatus.Cancelled;
        EndDate = utcNow;
        AutoRenew = false;
        UpdatedAt = utcNow;
    }

    public void MarkPastDue(DateTime utcNow)
    {
        if (Status != SubscriptionStatus.Active)
            throw new DomainException(
                "SUBSCRIPTION_NOT_ACTIVE",
                "Only an active subscription can become past due.");

        Status = SubscriptionStatus.PastDue;
        UpdatedAt = utcNow;
    }

    public void MarkExpired(DateTime utcNow)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new DomainException(
                "SUBSCRIPTION_ALREADY_ENDED",
                "Subscription has already ended.");

        Status = SubscriptionStatus.Expired;
        EndDate ??= utcNow;
        AutoRenew = false;
        UpdatedAt = utcNow;
    }

    public void SetAutoRenew(bool autoRenew, DateTime utcNow)
    {
        if (Status is not (
            SubscriptionStatus.Active or
            SubscriptionStatus.Trial or
            SubscriptionStatus.PastDue))
        {
            throw new DomainException(
                "SUBSCRIPTION_NOT_MODIFIABLE",
                "Ended subscriptions cannot change renewal settings.");
        }

        if (AutoRenew == autoRenew)
            return;

        AutoRenew = autoRenew;
        UpdatedAt = utcNow;
    }

    public bool IsEntitled(DateTime utcNow)
    {
        return Status is (
            SubscriptionStatus.Trial or
            SubscriptionStatus.Active or
            SubscriptionStatus.PastDue)
            && StartDate <= utcNow
            && (EndDate is null || EndDate > utcNow);
    }
}
