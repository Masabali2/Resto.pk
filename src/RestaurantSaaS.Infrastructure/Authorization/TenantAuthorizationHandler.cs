using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Infrastructure.Authorization;

/// <summary>
/// Single handler family for all tenant-scoped authorization. Answers the five
/// questions from requirement #10: who is the user, which tenant is active,
/// do they belong to it, what role do they hold there, does that role grant the
/// requested permission. Membership + role are read from StaffMemberships on
/// every evaluation (never from token claims).
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
        CancellationToken cancellationToken)
    {
        if (!await HasActiveMembershipAsync(cancellationToken))
            return;

        var membership = await GetMembershipAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        if (await HasActiveMembershipAsync(cancellationToken))
            context.Succeed(requirement);
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantOwnerRequirement requirement,
        CancellationToken cancellationToken)
    {
        var membership = await GetMembershipAsync(cancellationToken);
        if (membership?.RoleName == Permissions.TenantRoles.Owner)
            context.Succeed(requirement);
    }

    private async Task<bool> HasActiveMembershipAsync(CancellationToken ct) =>
        await GetMembershipAsync(ct) is not null;

    private async Task<TenantMembershipInfo?> GetMembershipAsync(CancellationToken ct)
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
public sealed class PlatformAdminOnlyHandler(UserManager<User> userManager)
    : AuthorizationHandler<PlatformAdminOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminOnlyRequirement requirement,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !Guid.TryParse(userId, out var id))
            return;

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is not null &&
            await userManager.IsInRoleAsync(user, Permissions.PlatformRoles.PlatformAdmin))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class PlatformAdminOnlyRequirement : IAuthorizationRequirement;
