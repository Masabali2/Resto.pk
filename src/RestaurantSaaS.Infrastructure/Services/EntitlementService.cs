using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Entitlements;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class EntitlementService(ApplicationDbContext db) : IEntitlementService
{
    public async Task EnsureCanAddAsync(Guid tenantId, EntitlementResource resource, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var current = await db.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Status,
                s.StartDate,
                s.EndDate,
                PlanMaxStaff = s.Plan.MaxStaff,
                PlanMaxMenuItems = s.Plan.MaxMenuItems
            })
            .FirstOrDefaultAsync(ct);

        if (current is null || !IsEntitled(current.Status, current.StartDate, current.EndDate, now))
            return;

        var limit = resource switch
        {
            EntitlementResource.StaffMembers => current.PlanMaxStaff,
            EntitlementResource.MenuItems => current.PlanMaxMenuItems,
            _ => 0
        };

        if (limit <= 0)
            return;

        var used = resource switch
        {
            EntitlementResource.StaffMembers => await db.StaffMemberships
                .CountAsync(m => m.TenantId == tenantId &&
                                 m.Status != MembershipStatus.Removed &&
                                 m.Status != MembershipStatus.Suspended, ct),
            EntitlementResource.MenuItems => await db.MenuItems
                .CountAsync(i => !i.IsDeleted && i.Category.Menu.TenantId == tenantId, ct),
            _ => 0
        };

        if (used >= limit)
        {
            throw new ConflictException(
                $"PLAN_LIMIT_REACHED: The current subscription plan allows at most {limit} {resource.ToString()}. Upgrade the plan to add more.");
        }
    }

    private static bool IsEntitled(SubscriptionStatus status, DateTime startDate, DateTime? endDate, DateTime now)
    {
        return status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.PastDue
               && startDate <= now
               && (endDate is null || endDate > now);
    }
}
