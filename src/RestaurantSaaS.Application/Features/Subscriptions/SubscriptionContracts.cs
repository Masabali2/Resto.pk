using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;

namespace RestaurantSaaS.Application.Features.Subscriptions;

public interface ISubscriptionPlanService
{
    Task<PagedResult<SubscriptionPlanDto>> ListAsync(
        bool onlyActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<SubscriptionPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default);

    Task<SubscriptionPlanDto> UpdateLimitsAsync(Guid id, UpdateSubscriptionPlanLimitsRequest request, CancellationToken ct = default);

    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task<PlatformSubscriptionOverviewDto> GetPlatformOverviewAsync(CancellationToken ct = default);

    Task<PagedResult<SubscriptionDto>> ListForPlatformAsync(PlatformSubscriptionQuery query, CancellationToken ct = default);

    Task<SubscriptionDto> GetByIdForPlatformAsync(Guid id, CancellationToken ct = default);
}

public interface ITenantSubscriptionService
{
    /// <summary>
    /// Complete subscription state of the active workspace: workspace status,
    /// current subscription, trial eligibility and the available plans.
    /// Available while the workspace is pending and after a subscription has
    /// lapsed, so the upgrade experience is always reachable.
    /// </summary>
    Task<CurrentSubscriptionStateDto> GetMyStateAsync(CancellationToken ct = default);

    /// <summary>Plans that are currently offered to workspaces.</summary>
    Task<IReadOnlyList<SubscriptionPlanDto>> ListAvailablePlansAsync(CancellationToken ct = default);

    /// <summary>Whether the active workspace may still consume its free trial.</summary>
    Task<TrialEligibilityDto> GetTrialEligibilityAsync(CancellationToken ct = default);

    Task<SubscriptionDto> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriptionDto>> ListForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Starts the 30-day free trial of the active workspace and activates the
    /// workspace in the same transaction.
    /// </summary>
    Task<SubscriptionDto> StartTrialAsync(CancellationToken ct = default);

    Task<SubscriptionDto> ChangePlanAsync(Guid subscriptionId, ChangeTenantSubscriptionPlanRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> CancelAsync(Guid subscriptionId, CancellationToken ct = default);

    Task<SubscriptionDto> SetStatusAsync(Guid subscriptionId, SetSubscriptionStatusRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> SetAutoRenewAsync(Guid subscriptionId, SetAutoRenewRequest request, CancellationToken ct = default);
}
