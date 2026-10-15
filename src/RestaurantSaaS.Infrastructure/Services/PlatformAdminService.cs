using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Platform.DTOs;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class PlatformAdminService(
    ApplicationDbContext db,
    UserManager<User> userManager) : IPlatformAdminService
{
    public async Task<PlatformDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var tenantsByStatus = await db.Tenants.AsNoTracking()
                .IgnoreQueryFilters()
                .GroupBy(t => new { t.Status, t.IsDeleted })
                .Select(g => new { g.Key.Status, g.Key.IsDeleted, Count = g.Count() })
                .ToListAsync(ct);

            var totalUsers = await db.Users.IgnoreQueryFilters().CountAsync(ct);

            var totalRestaurants = await db.Restaurants.AsNoTracking()
                .IgnoreQueryFilters()
                .CountAsync(r => !r.IsDeleted, ct);

            var totalStaff = await db.StaffMemberships.AsNoTracking()
                .IgnoreQueryFilters()
                .CountAsync(m => m.Status != MembershipStatus.Removed, ct);

            var subscriptionsByStatus = await db.Subscriptions.AsNoTracking()
                .IgnoreQueryFilters()
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            static int Sum(IEnumerable<(TenantStatus Status, bool IsDeleted, int Count)> rows, TenantStatus status, bool deleted) =>
                rows.Where(r => r.Status == status && r.IsDeleted == deleted).Sum(r => r.Count);

            var tenantRows = tenantsByStatus
                .Select(r => (r.Status, r.IsDeleted, r.Count))
                .ToList();

            return new PlatformDashboardDto(
                TotalTenants: tenantRows.Sum(r => r.Item3),
                ActiveTenants: Sum(tenantRows, TenantStatus.Active, false),
                SuspendedTenants: Sum(tenantRows, TenantStatus.Suspended, false),
                CancelledTenants: Sum(tenantRows, TenantStatus.Cancelled, false),
                PendingTenants: Sum(tenantRows, TenantStatus.Pending, false),
                DeletedTenants: tenantRows.Where(r => r.IsDeleted).Sum(r => r.Count),
                TotalUsers: totalUsers,
                TotalRestaurants: totalRestaurants,
                TotalStaffMemberships: totalStaff,
                TotalSubscriptions: subscriptionsByStatus.Sum(r => r.Count),
                SubscriptionsByStatus: subscriptionsByStatus.ToDictionary(
                    r => r.Status.ToString(), r => r.Count));
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    public async Task<PagedResult<PlatformUserDto>> ListUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) ||
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.FirstName != null && u.FirstName.Contains(term)) ||
                (u.LastName != null && u.LastName.Contains(term)));
        }

        query = query.OrderBy(u => u.CreatedAt);

        var total = await query.CountAsync(ct);

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmailConfirmed,
                u.LockoutEnabled,
                u.LockoutEnd,
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync(ct);

        var roleMap = await BuildRoleMapAsync(users.Select(u => u.Id).ToList(), ct);

        var items = users.Select(u => new PlatformUserDto(
            u.Id, u.UserName, u.Email, u.FirstName, u.LastName,
            u.EmailConfirmed, u.LockoutEnabled, u.LockoutEnd,
            u.CreatedAt, u.LastLoginAt,
            roleMap.TryGetValue(u.Id, out var roles)
                ? roles.Platform
                : Array.Empty<string>(),
            roleMap.TryGetValue(u.Id, out var t)
                ? t.Tenant
                : Array.Empty<string>())).ToList();

        return new PagedResult<PlatformUserDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<PlatformUserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmailConfirmed,
                u.LockoutEnabled,
                u.LockoutEnd,
                u.CreatedAt,
                u.LastLoginAt
            })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return null;

        var roleMap = await BuildRoleMapAsync([user.Id], ct);
        roleMap.TryGetValue(user.Id, out var roles);

        return new PlatformUserDto(
            user.Id, user.UserName, user.Email, user.FirstName, user.LastName,
            user.EmailConfirmed, user.LockoutEnabled, user.LockoutEnd,
            user.CreatedAt, user.LastLoginAt,
            roles.Platform ?? Array.Empty<string>(),
            roles.Tenant ?? Array.Empty<string>());
    }


    public async Task SetUserLockoutAsync(
        Guid userId,
        bool lockedOutUntilNextSignIn,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User", userId);

        IdentityResult result;

        if (lockedOutUntilNextSignIn)
        {
            result = await userManager.SetLockoutEnabledAsync(user, true);

            if (result.Succeeded)
            {
                result = await userManager.SetLockoutEndDateAsync(
                    user,
                    DateTimeOffset.MaxValue);
            }
        }
        else
        {
            result = await userManager.SetLockoutEndDateAsync(user, null);

            if (result.Succeeded)
            {
                result = await userManager.ResetAccessFailedCountAsync(user);
            }
        }

        if (!result.Succeeded)
        {
            throw new ConflictException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }


    public async Task<PlatformTenantDetailDto> GetTenantDetailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var tenant = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Id == tenantId)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.Status,
                    t.IsDeleted,
                    t.CreatedAt,
                    t.UpdatedAt,
                    RestaurantIds = t.Restaurants.Where(r => !r.IsDeleted).Select(r => r.Id).ToList(),
                    CurrentSubscription = t.Subscriptions
                        .OrderByDescending(s => s.StartDate)
                        .Select(s => new { s.Id, PlanName = s.Plan.Name, s.Status })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            return new PlatformTenantDetailDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status.ToString(),
                tenant.IsDeleted,
                tenant.CreatedAt,
                tenant.UpdatedAt,
                tenant.RestaurantIds,
                tenant.CurrentSubscription?.Id,
                tenant.CurrentSubscription?.PlanName,
                tenant.CurrentSubscription?.Status.ToString());
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    private async Task<Dictionary<Guid, (IReadOnlyList<string> Platform, IReadOnlyList<string> Tenant)>> BuildRoleMapAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        var map = new Dictionary<Guid, (IReadOnlyList<string> Platform, IReadOnlyList<string> Tenant)>();

        var platformRoles = await db.UserRoles.AsNoTracking().IgnoreQueryFilters()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles.AsNoTracking().IgnoreQueryFilters(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync(ct);

        var tenantRoles = await db.StaffMemberships.AsNoTracking().IgnoreQueryFilters()
            .Where(m => userIds.Contains(m.UserId) && m.Status == MembershipStatus.Active)
            .Select(m => new { m.UserId, RoleName = m.Role.Name })
            .ToListAsync(ct);

        foreach (var id in userIds)
        {
            var p = platformRoles.Where(r => r.UserId == id && r.RoleName != null).Select(r => r.RoleName!).Distinct().ToList();
            var t = tenantRoles.Where(r => r.UserId == id && r.RoleName != null).Select(r => r.RoleName!).Distinct().ToList();
            map[id] = (p, t);
        }

        return map;
    }
}
