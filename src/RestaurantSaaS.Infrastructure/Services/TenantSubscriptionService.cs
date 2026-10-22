using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class TenantSubscriptionService(
    ApplicationDbContext db,
    TenantAccessGuard accessGuard) : ITenantSubscriptionService
{
    public async Task<CurrentSubscriptionStateDto> GetMyStateAsync(CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.TenantView, ct , requireEntitlement: false);

        var current = await LoadCurrentSummaryAsync(tenantId, ct);

        if (current is null)
        {
            var plans = await db.SubscriptionPlans.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return new CurrentSubscriptionStateDto(
                false,
                null,
                new PagedResult<SubscriptionPlanDto>
                {
                    Items = plans.Select(SubscriptionPlanService.ToDtoPublic).ToList(),
                    Page = 1,
                    PageSize = plans.Count,
                    TotalCount = plans.Count
                });
        }

        return new CurrentSubscriptionStateDto(true, current, null);
    }

    public async Task<SubscriptionDto> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAdminAsync(tenantId, Permissions.TenantView, ct);

        var subscription = await LoadLatestAsync(tenantId, track: false, ct)
                           ?? throw new NotFoundException("The tenant has no subscription.");

        return ToDto(subscription);
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAdminAsync(tenantId, Permissions.TenantView, ct);

        var history = await db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .Include(s => s.Plan)
            .ToListAsync(ct);

        return history.Select(ToDto).ToList();
    }

    public async Task<SubscriptionDto> StartAsync(
    StartTenantSubscriptionRequest request,
    CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsureTrialOnboardingAccessAsync(ct);

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new NotFoundException("SubscriptionPlan", request.PlanId);

        if (!plan.IsActive)
        {
            throw new DomainException(
                "SUBSCRIPTION_PLAN_UNAVAILABLE",
                "This plan is not available for selection.");
        }

        var hasExistingSubscription = await db.Subscriptions
            .AnyAsync(
                s => s.TenantId == tenantId &&
                     (s.Status == SubscriptionStatus.Trial ||
                      s.Status == SubscriptionStatus.Active ||
                      s.Status == SubscriptionStatus.PastDue),
                ct);

        if (hasExistingSubscription)
        {
            throw new ConflictException(
                "This tenant already has an active or trial subscription.");
        }

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(
                t => t.Id == tenantId &&
                     t.Status == TenantStatus.Pending &&
                     !t.IsDeleted,
                ct)
            ?? throw new DomainException(
                "TENANT_NOT_ELIGIBLE_FOR_TRIAL",
                "Only a pending tenant can start its initial trial.");

        var now = DateTime.UtcNow;
        var endDate = now.AddDays(30);

        var subscription = Subscription.StartTrial(
            tenantId,
            plan.Id,
            now,
            endDate);

        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        db.Subscriptions.Add(subscription);
        tenant.ChangeStatus(TenantStatus.Active);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var savedSubscription = await db.Subscriptions
            .Include(s => s.Plan)
            .FirstAsync(s => s.Id == subscription.Id, ct);

        return ToDto(savedSubscription);
    }

    public async Task<SubscriptionDto> ChangePlanAsync(
     Guid subscriptionId,
     ChangeTenantSubscriptionPlanRequest request,
     CancellationToken ct = default)
    {
        var subscription = await LoadOwnedAsync(
            subscriptionId,
            Permissions.TenantUpdate,
            ct);

        if (subscription.Status is SubscriptionStatus.Cancelled
            or SubscriptionStatus.Expired)
        {
            throw new DomainException(
                "SUBSCRIPTION_ALREADY_ENDED",
                "An ended subscription cannot change plans.");
        }

        var newPlan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.NewPlanId, ct)
            ?? throw new NotFoundException(
                "SubscriptionPlan",
                request.NewPlanId);

        if (!newPlan.IsActive)
        {
            throw new DomainException(
                "SUBSCRIPTION_PLAN_UNAVAILABLE",
                "This plan is not available for selection.");
        }

        if (newPlan.Id == subscription.SubscriptionPlanId)
        {
            throw new DomainException(
                "SUBSCRIPTION_PLAN_UNCHANGED",
                "The subscription is already on this plan.");
        }

        throw new DomainException(
            "SUBSCRIPTION_PAYMENT_REQUIRED",
            "Changing plans requires a supported payment flow. Plan changes are temporarily unavailable.");
    }
    public async Task<SubscriptionDto> CancelAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var subscription = await LoadOwnedAsync(subscriptionId, Permissions.TenantUpdate, ct);

        subscription.Cancel(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(subscription);
    }

    public async Task<SubscriptionDto> SetStatusAsync(Guid subscriptionId, SetSubscriptionStatusRequest request, CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.TenantUpdate, ct);

        var subscription = await LoadOwnedAsync(subscriptionId, tenantId, ct);

        if (!Enum.TryParse<SubscriptionStatus>(request.Status, ignoreCase: false, out var target))
            throw new DomainException("SUBSCRIPTION_STATUS_INVALID", "Unknown subscription status.");

        var now = DateTime.UtcNow;

        switch (target)
        {
            case SubscriptionStatus.PastDue:
                subscription.MarkPastDue(now);
                break;
            case SubscriptionStatus.Expired:
                subscription.MarkExpired(now);
                break;
            case SubscriptionStatus.Cancelled:
                subscription.Cancel(now);
                break;
            default:
                throw new DomainException(
                    "SUBSCRIPTION_TRANSITION_UNSUPPORTED",
                    $"Transition to '{target}' is not supported by the subscription lifecycle.");
        }

        await db.SaveChangesAsync(ct);

        return ToDto(subscription);
    }

    public async Task<SubscriptionDto> SetAutoRenewAsync(Guid subscriptionId, SetAutoRenewRequest request, CancellationToken ct = default)
    {
        var subscription = await LoadOwnedAsync(subscriptionId, Permissions.TenantUpdate, ct);

        subscription.SetAutoRenew(request.AutoRenew, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(subscription);
    }

    // ---------------- helpers ----------------

    private async Task RequireTenantAdminAsync(Guid targetTenantId, string permission, CancellationToken ct)
    {
        var actorTenantId = await accessGuard.EnsurePermissionAsync(permission, ct , requireEntitlement: false);

        if (targetTenantId != actorTenantId)
            throw new CrossTenantAccessException();
    }

    private async Task<Subscription> LoadOwnedAsync(Guid id, string permission, CancellationToken ct)
    {
        var tenantId = await accessGuard.EnsurePermissionAsync(permission, ct, requireEntitlement: false);
        return await LoadOwnedAsync(id, tenantId, ct);
    }

    private async Task<Subscription> LoadOwnedAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Subscription", id);

        if (subscription.TenantId != tenantId)
            throw new CrossTenantAccessException();

        return subscription;
    }

    private async Task<TenantSubscriptionSummaryDto?> LoadCurrentSummaryAsync(Guid tenantId, CancellationToken ct)
    {
        var subscription = await LoadLatestAsync(tenantId, track: false, ct);
        if (subscription is null)
            return null;

        return new TenantSubscriptionSummaryDto(
            subscription.Id,
            subscription.SubscriptionPlanId,
            subscription.Plan.Name,
            subscription.Status.ToString(),
            subscription.StartDate,
            subscription.EndDate,
            subscription.AutoRenew,
            subscription.IsEntitled(DateTime.UtcNow),
            subscription.Plan.MaxStaff,
            subscription.Plan.MaxMenuItems,
            subscription.Plan.MaxOrdersPerMonth);
    }

    private async Task<Subscription?> LoadLatestAsync(Guid tenantId, bool track, CancellationToken ct)
    {
        var query = db.Subscriptions.AsNoTracking();
        if (track) query = db.Subscriptions;

        return await query
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(ct);
    }

    private static DateTime? ComputeEndDate(DateTime start, BillingInterval interval) => interval switch
    {
        BillingInterval.Monthly => start.AddDays(30),
        BillingInterval.Quarterly => start.AddDays(91),
        BillingInterval.Yearly => start.AddDays(365),
        _ => null
    };

    internal static SubscriptionDto ToDto(Subscription s) => new(
        s.Id, s.TenantId, s.SubscriptionPlanId, s.Plan?.Name ?? string.Empty,
        s.Status.ToString(), s.StartDate, s.EndDate, s.AutoRenew,
        s.IsEntitled(DateTime.UtcNow), s.CreatedAt, s.UpdatedAt);

    
}
