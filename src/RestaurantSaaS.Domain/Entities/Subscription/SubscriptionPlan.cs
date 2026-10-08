using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Subscriptions;

/// <summary>
/// Platform-defined SaaS plan (catalog). NOT tenant-owned — shared across all
/// tenants, therefore intentionally has no TenantId (requirement #38).
/// Limits (MaxStaff/MaxMenuItems/MaxOrdersPerMonth) are enforced by the
/// subscription-limit checks in the application layer.
/// </summary>
public class SubscriptionPlan : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Price stored as decimal column; Money VO used at the API boundary.</summary>
    public decimal Price { get; private set; }

    /// <summary>ISO currency code of <see cref="Price"/>.</summary>
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public BillingInterval BillingInterval { get; private set; }

    /// <summary>0 means unlimited.</summary>
    public int MaxStaff { get; private set; }

    public int MaxMenuItems { get; private set; }
    public int MaxOrdersPerMonth { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static SubscriptionPlan Create(
        string name,
        Money price,
        BillingInterval billingInterval,
        int maxStaff,
        int maxMenuItems,
        int maxOrdersPerMonth,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("PLAN_NAME_REQUIRED", "Plan name is required.");

        if (maxStaff < 0 || maxMenuItems < 0 || maxOrdersPerMonth < 0)
            throw new DomainException("PLAN_LIMITS_INVALID", "Plan limits cannot be negative (use 0 for unlimited).");

        return new SubscriptionPlan
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = price.Amount,
            Currency = price.Currency,
            BillingInterval = billingInterval,
            MaxStaff = maxStaff,
            MaxMenuItems = maxMenuItems,
            MaxOrdersPerMonth = maxOrdersPerMonth,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateLimits(int maxStaff, int maxMenuItems, int maxOrdersPerMonth)
    {
        if (maxStaff < 0 || maxMenuItems < 0 || maxOrdersPerMonth < 0)
            throw new DomainException("PLAN_LIMITS_INVALID", "Plan limits cannot be negative (use 0 for unlimited).");

        MaxStaff = maxStaff;
        MaxMenuItems = maxMenuItems;
        MaxOrdersPerMonth = maxOrdersPerMonth;
        UpdatedAt = DateTime.UtcNow;
    }

    public Money GetPrice() => Money.New(Price, Currency);

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }
}
