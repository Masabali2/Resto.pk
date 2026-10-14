using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class SubscriptionPlanService(ApplicationDbContext db) : ISubscriptionPlanService
{
    public async Task<PagedResult<SubscriptionPlanDto>> ListAsync(
        bool onlyActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.SubscriptionPlans.AsNoTracking();

        if (onlyActive)
            query = query.Where(p => p.IsActive);

        query = query.OrderBy(p => p.Name);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SubscriptionPlanDto>
        {
            Items = items.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<SubscriptionPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw new NotFoundException("SubscriptionPlan", id);

        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BillingInterval>(request.BillingInterval, ignoreCase: false, out var interval))
            throw new DomainException("PLAN_BILLING_INTERVAL_INVALID", "The specified billing interval is invalid.");

        var exists = await db.SubscriptionPlans.AnyAsync(p => p.Name == request.Name.Trim(), ct);
        if (exists)
            throw new ConflictException($"A plan named '{request.Name}' already exists.");

        var money = Money.New(request.Price, request.Currency);

        var plan = SubscriptionPlan.Create(
            request.Name,
            money,
            interval,
            request.MaxStaff,
            request.MaxMenuItems,
            request.MaxOrdersPerMonth,
            request.Description);

        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> UpdateLimitsAsync(Guid id, UpdateSubscriptionPlanLimitsRequest request, CancellationToken ct = default)
    {
        var plan = await LoadAsync(id, ct);

        plan.UpdateLimits(request.MaxStaff, request.MaxMenuItems, request.MaxOrdersPerMonth);
        await db.SaveChangesAsync(ct);

        return ToDto(plan);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var plan = await LoadAsync(id, ct);

        plan.SetActive(isActive);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SubscriptionPlan> LoadAsync(Guid id, CancellationToken ct)
    {
        return await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct)
               ?? throw new NotFoundException("SubscriptionPlan", id);
    }

    public async Task<PlatformSubscriptionOverviewDto> GetPlatformOverviewAsync(CancellationToken ct = default)
    {
        var totalTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters().CountAsync(ct);
        var activeTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(t => t.Status == Domain.Enums.TenantStatus.Active && !t.IsDeleted, ct);
        var suspendedTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(t => t.Status == Domain.Enums.TenantStatus.Suspended, ct);
        var cancelledTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(t => t.Status == Domain.Enums.TenantStatus.Cancelled, ct);
        var pendingTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(t => t.Status == Domain.Enums.TenantStatus.Pending, ct);
        var deletedTenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(t => t.IsDeleted, ct);

        var totalRestaurants = await db.Restaurants.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(r => !r.IsDeleted, ct);
        var totalStaff = await db.StaffMemberships.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(m => m.Status != Domain.Enums.MembershipStatus.Removed, ct);

        var subscriptions = await db.Subscriptions.AsNoTracking().IgnoreQueryFilters()
            .Select(s => new
            {
                s.Status,
                s.SubscriptionPlanId,
                PlanName = s.Plan.Name,
                PlanActive = s.Plan.IsActive
            })
            .ToListAsync(ct);

        var byStatus = subscriptions
            .GroupBy(s => s.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var planDistribution = subscriptions
            .GroupBy(s => (s.SubscriptionPlanId, s.PlanName, s.PlanActive))
            .Select(g => new PlanDistributionDto(
                g.Key.SubscriptionPlanId,
                g.Key.PlanName,
                g.Key.PlanActive,
                g.Count()))
            .OrderByDescending(d => d.SubscriptionCount)
            .ThenBy(d => d.PlanName, StringComparer.Ordinal)
            .ToList();

        return new PlatformSubscriptionOverviewDto(
            totalTenants,
            activeTenants,
            suspendedTenants,
            cancelledTenants,
            pendingTenants,
            deletedTenants,
            totalRestaurants,
            totalStaff,
            subscriptions.Count,
            byStatus,
            planDistribution);
    }

    public async Task<PagedResult<SubscriptionDto>> ListForPlatformAsync(
        PlatformSubscriptionQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Subscriptions.AsNoTracking().IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<SubscriptionStatus>(query.Status, ignoreCase: false, out var status))
                throw new DomainException(
                    "SUBSCRIPTION_STATUS_INVALID",
                    "Unknown subscription status filter.");

            q = q.Where(s => s.Status == status);
        }

        if (query.PlanId.HasValue)
            q = q.Where(s => s.SubscriptionPlanId == query.PlanId.Value);

        if (query.TenantId.HasValue)
            q = q.Where(s => s.TenantId == query.TenantId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(s => s.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(s => s.Plan)
            .ToListAsync(ct);

        return new PagedResult<SubscriptionDto>
        {
            Items = items.Select(TenantSubscriptionService.ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<SubscriptionDto> GetByIdForPlatformAsync(Guid id, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.AsNoTracking().IgnoreQueryFilters()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Subscription", id);

        return TenantSubscriptionService.ToDto(subscription);
    }

    internal static SubscriptionPlanDto ToDtoPublic(SubscriptionPlan p) => new(
        p.Id, p.Name, p.Description, p.Price, p.Currency, p.BillingInterval.ToString(),
        p.MaxStaff, p.MaxMenuItems, p.MaxOrdersPerMonth, p.IsActive, p.CreatedAt, p.UpdatedAt);

    private static SubscriptionPlanDto ToDto(SubscriptionPlan p) => ToDtoPublic(p);
}
