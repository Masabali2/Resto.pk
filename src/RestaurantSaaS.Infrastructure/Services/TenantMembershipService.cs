using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;


public sealed class TenantMembershipService(ApplicationDbContext db) : ITenantMembershipService
{
    public async Task<TenantMembershipInfo?> GetMembershipAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            return await db.StaffMemberships
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.TenantId == tenantId)
                .Select(m => new TenantMembershipInfo(
                    m.Id,
                    m.TenantId,
                    m.Tenant.Name,
                    m.Tenant.Slug,
                    m.RoleId,
                    m.Role.Name!,
                    (int)m.Status))
                .FirstOrDefaultAsync(cancellationToken);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    public async Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var rows = await db.StaffMemberships
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
                .OrderBy(m => m.Tenant.Name)
                .Select(m => new TenantMembershipInfo(
                    m.Id,
                    m.TenantId,
                    m.Tenant.Name,
                    m.Tenant.Slug,
                    m.RoleId,
                    m.Role.Name!,
                    (int)m.Status))
                .ToListAsync(cancellationToken);

            return rows;
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }
}
