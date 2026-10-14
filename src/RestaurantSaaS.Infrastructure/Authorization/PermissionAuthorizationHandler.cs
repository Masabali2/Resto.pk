
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(
    ApplicationDbContext db)
    : AuthorizationHandler<PermissionRequirement>
{
    // This must match the active-tenant claim issued by our API.
    private const string TenantIdClaim = "tenant_id";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Platform permissions are separate from tenant permissions.
        if (Permissions.AllPlatformPermissions.Contains(
                requirement.Permission))
        {
            if (context.User.IsInRole(
                    Permissions.PlatformRoles.PlatformAdmin))
            {
                context.Succeed(requirement);
            }

            return;
        }

        // Tenant authorization requires an active tenant context.
        var tenantClaim =
            context.User.FindFirst(TenantIdClaim)?.Value;

        var userIdClaim =
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(tenantClaim, out var tenantId) ||
            !Guid.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        // Resolve the user's role through their tenant membership.
        var membership = await db.StaffMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId &&
                m.UserId == userId);

        if (membership is null)
        {
            return;
        }

        var role = await db.Roles
            .AsNoTracking()
            .Where(r => r.Id == membership.RoleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            return;
        }

        if (Permissions.RolePermissions.TryGetValue(
                role, out var permissions) &&
            permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}