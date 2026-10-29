using RestaurantSaaS.Application.Common.Models;

namespace RestaurantSaaS.Application.Features.Subscriptions.DTOs;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string BillingInterval,
    int MaxStaff,
    int MaxMenuItems,
    int MaxOrdersPerMonth,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateSubscriptionPlanRequest(
    string Code,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string BillingInterval,
    int MaxStaff,
    int MaxMenuItems,
    int MaxOrdersPerMonth);

public sealed record UpdateSubscriptionPlanLimitsRequest(
    int MaxStaff,
    int MaxMenuItems,
    int MaxOrdersPerMonth);

public sealed record SubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    bool AutoRenew,
    bool IsEntitled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record TenantSubscriptionSummaryDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    bool AutoRenew,
    bool IsEntitled,
    int MaxStaff,
    int MaxMenuItems,
    int MaxOrdersPerMonth,
    decimal Price,
    string Currency,
    string BillingInterval);

/// <summary>
/// Whether the active workspace may still consume its free trial.
///
/// <see cref="TrialUsed"/> stays true forever once a subscription row exists
/// for the workspace, so a consumed trial can never be replayed.
/// </summary>
public sealed record TrialEligibilityDto(
    bool Eligible,
    bool TrialUsed,
    int TrialDurationDays,
    string? Reason);

/// <summary>
/// Complete subscription state of the active workspace.
///
/// This is the single authority the client uses: it carries the workspace
/// status, the current subscription, the trial eligibility and the plans that
/// are actually available, so the UI never has to guess.
/// </summary>
public sealed record CurrentSubscriptionStateDto(
    Guid TenantId,
    string TenantName,
    string TenantStatus,
    bool HasSubscription,
    TenantSubscriptionSummaryDto? Subscription,
    TrialEligibilityDto Trial,
    IReadOnlyList<SubscriptionPlanDto> AvailablePlans,
    DateTime ServerTimeUtc);

public sealed record ChangeTenantSubscriptionPlanRequest(Guid NewPlanId);

public sealed record SetSubscriptionStatusRequest(string Status);

public sealed record SetAutoRenewRequest(bool AutoRenew);

public sealed record PlatformSubscriptionOverviewDto(
    int TotalTenants,
    int ActiveTenants,
    int SuspendedTenants,
    int CancelledTenants,
    int PendingTenants,
    int DeletedTenants,
    int TotalRestaurants,
    int TotalStaffMemberships,
    int TotalSubscriptions,
    IReadOnlyDictionary<string, int> SubscriptionsByStatus,
    IReadOnlyList<PlanDistributionDto> PlanDistribution);

public sealed record PlanDistributionDto(
    Guid PlanId,
    string PlanName,
    bool PlanIsActive,
    int SubscriptionCount);

public sealed record PlatformSubscriptionQuery(
    string? Status = null,
    Guid? PlanId = null,
    Guid? TenantId = null,
    int Page = 1,
    int PageSize = 20);
