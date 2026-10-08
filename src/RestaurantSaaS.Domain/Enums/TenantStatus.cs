namespace RestaurantSaaS.Domain.Enums;

public enum TenantStatus
{
    /// <summary>Created but not yet usable (onboarding incomplete).</summary>
    Pending = 1,

    /// <summary>Active SaaS customer.</summary>
    Active = 2,

    /// <summary>Suspended by the platform (billing/violation). Data retained.</summary>
    Suspended = 3,

    /// <summary>Cancelled; enters a data-retention period before purge.</summary>
    Cancelled = 4
}
