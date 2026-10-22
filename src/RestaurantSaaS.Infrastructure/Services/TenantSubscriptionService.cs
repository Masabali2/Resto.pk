using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Subscription handling for the ACTIVE workspace.
///
/// The workspace is never taken from the request: it is derived from the
/// server-resolved tenant context on every operation, and every read or write
/// is additionally re-verified against the caller's live membership. A client
/// supplied tenant id is therefore never proof of authorization.
/// </summary>
public sealed class TenantSubscriptionService(
    ApplicationDbContext db,
    TenantAccessGuard accessGuard,
    TenantSubscriptionGate trialGate) : ITenantSubscriptionService
{
    // ---------------- state & eligibility ----------------

    public async Task<CurrentSubscriptionStateDto> GetMyStateAsync(CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsureTenantMemberAsync(Permissions.TenantView, ct);

        return await BuildStateAsync(tenantId, ct);
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListAvailablePlansAsync(CancellationToken ct = default)
    {
        await accessGuard.EnsureTenantMemberAsync(Permissions.TenantView, ct);

        return await LoadAvailablePlansAsync(ct);
    }

    public async Task<TrialEligibilityDto> GetTrialEligibilityAsync(CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsureTenantMemberAsync(Permissions.TenantView, ct);

        return await LoadTrialEligibilityAsync(tenantId, ct);
    }

    // ---------------- tenant scoped reads ----------------

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

    // ---------------- trial activation ----------------

    /// <summary>
    /// Starts the 30-day free trial of the active workspace.
    ///
    /// The subscription is created and the workspace is activated inside ONE
    /// transaction: either both happen or neither does. A workspace is never
    /// left active without a valid subscription, and a subscription is never
    /// left behind without an active workspace.
    ///
    /// Guards, in order:
    ///  1. the caller is an active member of the active workspace,
    ///  2. the caller is the workspace owner,
    ///  3. the workspace has not consumed its trial yet,
    ///  4. the workspace is still pending,
    ///  5. the free-trial plan is configured and active.
    /// </summary>
    public async Task<SubscriptionDto> StartTrialAsync(CancellationToken ct = default)
    {
        var tenantId = await accessGuard.EnsureTrialOwnerAsync(ct);

        await using var claim = await trialGate.AcquireAsync(tenantId, ct);

        var plan = await db.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Name == SubscriptionTrialPolicy.TrialPlanName && p.IsActive,
                ct)
            ?? throw new DomainException(
                "TRIAL_PLAN_NOT_CONFIGURED",
                "The free trial plan is not available. Please contact support.");

        var eligibility = await LoadTrialEligibilityAsync(tenantId, ct);

        if (!eligibility.Eligible)
        {
            throw new DomainException(
                "TRIAL_NOT_AVAILABLE",
                eligibility.Reason ?? "The free trial is not available for this workspace.");
        }

        var now = DateTime.UtcNow;
        var subscription = Subscription.StartTrial(
            tenantId,
            plan.Id,
            now,
            SubscriptionTrialPolicy.CalculateTrialEnd(now));

        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                // Re-read inside the transaction: the workspace state and the
                // trial consumption are verified again while the write lock
                // is held, so a request that waited for the gate cannot act on
                // a stale decision.
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new NotFoundException("Tenant", tenantId);

                if (tenant.Status != TenantStatus.Pending)
                {
                    throw new DomainException(
                        "TENANT_NOT_ELIGIBLE_FOR_TRIAL",
                        "Only a pending workspace can start its initial trial.");
                }

                var alreadyUsed = await db.Subscriptions
                    .AnyAsync(s => s.TenantId == tenantId, ct);

                if (alreadyUsed)
                {
                    throw new ConflictException(
                        "The free trial has already been used for this workspace.");
                }

                db.Subscriptions.Add(subscription);
                tenant.ChangeStatus(TenantStatus.Active);

                await db.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            });
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The filtered unique index rejected a second Trial/Active/PastDue
            // subscription for the same workspace (raised by a concurrent
            // request in another process).
            throw new ConflictException(
                "The free trial has already been started for this workspace.");
        }

        var savedSubscription = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstAsync(s => s.Id == subscription.Id, ct);

        return ToDto(savedSubscription);
    }

    // ---------------- management ----------------

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
        var tenantId = await accessGuard.EnsureTenantMemberAsync(Permissions.TenantUpdate, ct);

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

    private async Task<CurrentSubscriptionStateDto> BuildStateAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);

        var current = await LoadCurrentSummaryAsync(tenantId, ct);
        var trial = await LoadTrialEligibilityAsync(tenantId, ct);
        var plans = await LoadAvailablePlansAsync(ct);

        return new CurrentSubscriptionStateDto(
            tenant.Id,
            tenant.Name,
            tenant.Status.ToString(),
            current is not null,
            current,
            trial,
            plans,
            DateTime.UtcNow);
    }

    /// <summary>
    /// The trial of a workspace is consumed the moment a subscription row
    /// exists for it — regardless of the row's status. A cancelled or expired
    /// trial therefore stays part of the history and can never be replayed.
    /// </summary>
    private async Task<TrialEligibilityDto> LoadTrialEligibilityAsync(Guid tenantId, CancellationToken ct)
    {
        var trialUsed = await db.Subscriptions
            .AnyAsync(s => s.TenantId == tenantId, ct);

        if (trialUsed)
        {
            return new TrialEligibilityDto(
                false,
                true,
                SubscriptionTrialPolicy.TrialDurationDays,
                "The free trial has already been used for this workspace.");
        }

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
        {
            return new TrialEligibilityDto(
                false,
                false,
                SubscriptionTrialPolicy.TrialDurationDays,
                "The workspace could not be found.");
        }

        if (tenant.Status != TenantStatus.Pending)
        {
            return new TrialEligibilityDto(
                false,
                false,
                SubscriptionTrialPolicy.TrialDurationDays,
                tenant.Status switch
                {
                    TenantStatus.Active =>
                        "This workspace is already active and no longer awaiting its trial.",
                    TenantStatus.Suspended =>
                        "This workspace is suspended and cannot start a trial.",
                    TenantStatus.Cancelled =>
                        "This workspace has been cancelled.",
                    _ =>
                        "This workspace is not awaiting activation."
                });
        }

        return new TrialEligibilityDto(
            true,
            false,
            SubscriptionTrialPolicy.TrialDurationDays,
            null);
    }

    private async Task<IReadOnlyList<SubscriptionPlanDto>> LoadAvailablePlansAsync(CancellationToken ct)
    {
        var plans = await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return plans.Select(SubscriptionPlanService.ToDtoPublic).ToList();
    }

    private async Task RequireTenantAdminAsync(Guid targetTenantId, string permission, CancellationToken ct)
    {
        var actorTenantId = await accessGuard.EnsureTenantMemberAsync(permission, ct);

        if (targetTenantId != actorTenantId)
            throw new CrossTenantAccessException();
    }

    private async Task<Subscription> LoadOwnedAsync(Guid id, string permission, CancellationToken ct)
    {
        var tenantId = await accessGuard.EnsureTenantMemberAsync(permission, ct);
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is not null &&
        (ex.InnerException.Message.Contains("UX_Subscriptions", StringComparison.Ordinal) ||
         ex.InnerException.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) ||
         ex.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));

    internal static SubscriptionDto ToDto(Subscription s) => new(
        s.Id, s.TenantId, s.SubscriptionPlanId, s.Plan?.Name ?? string.Empty,
        s.Status.ToString(), s.StartDate, s.EndDate, s.AutoRenew,
        s.IsEntitled(DateTime.UtcNow), s.CreatedAt, s.UpdatedAt);
}
