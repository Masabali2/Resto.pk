using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Authoritative membership source. Reads StaffMemberships joined to Tenants and
/// Roles on every call — JWT claims are never consulted, so role changes take
/// effect immediately regardless of token age (Phase-2 requirement #13).
///
/// Membership queries run with tenant filters DISABLED because the caller is
/// asking "does user X belong to tenant Y" for an EXPLICIT tenant id — this is
/// the check that ESTABLISHES trust in a tenant, not one that assumes it.
/// </summary>
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
