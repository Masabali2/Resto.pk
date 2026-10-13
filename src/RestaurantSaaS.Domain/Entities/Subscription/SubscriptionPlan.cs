
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Subscriptions;

public class SubscriptionPlan : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public decimal Price { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public BillingInterval BillingInterval { get; private set; }

    public int MaxStaff { get; private set; }
    public int MaxMenuItems { get; private set; }
    public int MaxOrdersPerMonth { get; private set; }

    public bool IsActive { get; private set; } = true;

    private SubscriptionPlan()
    {
    }

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
            throw new DomainException(
                "PLAN_NAME_REQUIRED",
                "Plan name is required.");

        ArgumentNullException.ThrowIfNull(price);

        if (name.Trim().Length > 100)
            throw new DomainException(
                "PLAN_NAME_TOO_LONG",
                "Plan name cannot exceed 100 characters.");

        if (maxStaff < 0 || maxMenuItems < 0 || maxOrdersPerMonth < 0)
            throw new DomainException(
                "PLAN_LIMITS_INVALID",
                "Plan limits cannot be negative. Use 0 for unlimited.");

        if (!Enum.IsDefined(billingInterval))
            throw new DomainException(
                "PLAN_BILLING_INTERVAL_INVALID",
                "The specified billing interval is invalid.");

        var now = DateTime.UtcNow;

        return new SubscriptionPlan
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim(),
            Price = price.Amount,
            Currency = price.Currency,
            BillingInterval = billingInterval,
            MaxStaff = maxStaff,
            MaxMenuItems = maxMenuItems,
            MaxOrdersPerMonth = maxOrdersPerMonth,
            IsActive = true,
            CreatedAt = now
        };
    }

    public void UpdateLimits(
        int maxStaff,
        int maxMenuItems,
        int maxOrdersPerMonth)
    {
        if (maxStaff < 0 || maxMenuItems < 0 || maxOrdersPerMonth < 0)
            throw new DomainException(
                "PLAN_LIMITS_INVALID",
                "Plan limits cannot be negative. Use 0 for unlimited.");

        if (MaxStaff == maxStaff &&
            MaxMenuItems == maxMenuItems &&
            MaxOrdersPerMonth == maxOrdersPerMonth)
        {
            return;
        }

        MaxStaff = maxStaff;
        MaxMenuItems = maxMenuItems;
        MaxOrdersPerMonth = maxOrdersPerMonth;
        UpdatedAt = DateTime.UtcNow;
    }

    public Money GetPrice()
    {
        return Money.New(Price, Currency);
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }
}