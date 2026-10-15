using RestaurantSaaS.Application.Common.Models;

namespace RestaurantSaaS.Application.Features.Subscriptions.DTOs;

public sealed record SubscriptionPlanDto(
    Guid Id,
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
    int MaxOrdersPerMonth);

public sealed record CurrentSubscriptionStateDto(
    bool HasSubscription,
    TenantSubscriptionSummaryDto? Subscription,
    PagedResult<SubscriptionPlanDto>? AvailablePlans);

public sealed record StartTenantSubscriptionRequest(Guid PlanId);

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
