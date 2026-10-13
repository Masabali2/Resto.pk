using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Infrastructure.Authorization;

/// <summary>
/// Single handler covering all three tenant-scoped requirements. Answers the
/// five questions from requirement #10: who is the user, which tenant is active,
/// do they belong to it, what role do they hold there, does that role grant what
/// is requested. Membership + role are read from StaffMemberships on EVERY
/// evaluation — never from (possibly stale) JWT claims. Fail closed everywhere.
/// Registered once as IAuthorizationHandler for all three requirement types.
/// </summary>
public sealed class TenantAuthorizationHandler(
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantMembershipService membershipService)
    : AuthorizationHandler<TenantPermissionRequirement>,
      AuthorizationHandler<TenantMemberRequirement>,
      AuthorizationHandler<TenantOwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantPermissionRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(cancellationToken);
        if (membership is null)
            return;

        // Role name -> permission set from the single source of truth (Permissions.RolePermissions).
        if (Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) &&
            perms.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMemberRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (await GetActiveMembershipAsync(cancellationToken) is not null)
            context.Succeed(requirement);
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantOwnerRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveMembershipAsync(cancellationToken);
        if (membership?.RoleName == Permissions.TenantRoles.Owner)
            context.Succeed(requirement);
    }

    private async Task<TenantMembershipInfo?> GetActiveMembershipAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return null;

        if (!tenantContext.HasTenant)
            return null;

        var membership = await membershipService.GetMembershipAsync(userId, tenantContext.GetRequiredTenantId(), ct);

        // Only ACTIVE memberships authorize. Invited/Suspended/Removed => fail closed.
        return membership is not null && membership.Status == (int)MembershipStatus.Active
            ? membership
            : null;
    }
}

/// <summary>Platform-admin gate backed by GLOBAL Identity role membership (AspNetUserRoles), validated live.</summary>
public sealed class PlatformAdminOnlyHandler(Microsoft.AspNetCore.Identity.UserManager<User> userManager)
    : AuthorizationHandler<PlatformAdminOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminOnlyRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(userId, out var id))
            return;

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is not null && await userManager.IsInRoleAsync(user, Permissions.PlatformRoles.PlatformAdmin))
            context.Succeed(requirement);
    }
}
