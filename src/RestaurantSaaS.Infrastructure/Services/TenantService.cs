using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Tenants;
using RestaurantSaaS.Application.Features.Tenants.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class TenantService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantMembershipService membershipService) : ITenantService
{
    // ---------------- Onboarding (authorized flow only) ----------------

    public async Task<TenantAdminDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new NotAuthenticatedException();

        // Normalize + validate slug through the domain (single normalization source of truth).
        var tenant = Tenant.Create(request.Name, request.Slug);

        var ownerRoleId = await GetRequiredRoleIdAsync(Permissions.TenantRoles.Owner, ct);

        // TRANSACTION: tenant + initial Owner membership must exist together or not at all.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.Tenants.Add(tenant);
                db.StaffMemberships.Add(StaffMembership.Create(tenant.Id, userId, ownerRoleId));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Slug uniqueness is a DATABASE guarantee, not just app validation.
                throw new ConflictException($"The slug '{tenant.Slug}' is already taken.");
            }
        });

        return ToAdminDto(tenant);
    }

    // ---------------- Current-tenant operations ----------------

    public async Task<TenantDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var tenant = await GetCurrentUserTenantAsync(requireOperational: false, ct);
        return ToDto(tenant);
    }

    public async Task<TenantDto> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await GetCurrentUserTenantAsync(requireOperational: true, ct);

        tenant.Rename(request.Name);
        await db.SaveChangesAsync(ct);

        return ToDto(tenant);
    }

    // ---------------- Platform-admin operations ----------------

    public async Task<TenantAdminDto> GetByIdForPlatformAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Platform scope: explicit, audited bypass of tenant filters. This path is
        // reachable ONLY behind the PlatformAdminOnly policy at the API boundary.
        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var tenant = await db.Tenants.AsNoTracking()
                .IgnoreQueryFilters() // platform lookup must see soft-deleted tenants too
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            return ToAdminDto(tenant);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    public async Task<TenantAdminDto> ChangeStatusForPlatformAsync(Guid tenantId, string status, CancellationToken ct = default)
    {
        if (!Enum.TryParse<TenantStatus>(status, ignoreCase: false, out var newStatus) ||
            newStatus is TenantStatus.Pending)
        {
            throw new DomainException("TENANT_STATUS_INVALID", "Status must be Active, Suspended or Cancelled.");
        }

        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true;
        try
        {
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            tenant.ChangeStatus(newStatus);
            await db.SaveChangesAsync(ct);

            return ToAdminDto(tenant);
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    // ---------------- helpers ----------------

    /// <summary>
    /// Loads the CURRENT tenant after re-verifying the caller's active membership
    /// server-side — even though middleware already resolved the tenant, we never
    /// assume (requirement #22: query filters are not the only mechanism).
    /// </summary>
    private async Task<Tenant> GetCurrentUserTenantAsync(bool requireOperational, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);

        if (requireOperational)
            tenant.EnsureOperational();

        return tenant;
    }

    private async Task<Guid> GetRequiredRoleIdAsync(string roleName, CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == roleName, ct)
            ?? throw new ConflictException($"Tenant role '{roleName}' is not provisioned. Check database seeding.");
        return role.Id;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is not null &&
        (ex.InnerException.Message.Contains("IX_Tenants_Slug", StringComparison.Ordinal) ||
         ex.InnerException.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) ||
         ex.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));

    private static TenantDto ToDto(Tenant t) =>
        new(t.Id, t.Name, t.Slug, t.Status.ToString(), t.CreatedAt);

    private static TenantAdminDto ToAdminDto(Tenant t) =>
        new(t.Id, t.Name, t.Slug, t.Status.ToString(), t.IsDeleted, t.CreatedAt, t.UpdatedAt);
}
