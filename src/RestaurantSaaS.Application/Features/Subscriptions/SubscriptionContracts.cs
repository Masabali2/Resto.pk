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
    Task<CurrentSubscriptionStateDto> GetMyStateAsync(CancellationToken ct = default);

    Task<SubscriptionDto> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriptionDto>> ListForTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<SubscriptionDto> StartAsync(StartTenantSubscriptionRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> ChangePlanAsync(Guid subscriptionId, ChangeTenantSubscriptionPlanRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> CancelAsync(Guid subscriptionId, CancellationToken ct = default);

    Task<SubscriptionDto> SetStatusAsync(Guid subscriptionId, SetSubscriptionStatusRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> SetAutoRenewAsync(Guid subscriptionId, SetAutoRenewRequest request, CancellationToken ct = default);
}
