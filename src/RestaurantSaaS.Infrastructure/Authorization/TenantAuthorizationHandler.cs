using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Infrastructure.Authorization;

public sealed class TenantPermissionHandler(
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantMembershipService membershipService)
    : AuthorizationHandler<TenantPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantPermissionRequirement requirement)
    {
        var membership = await TenantAuthorization.GetActiveMembershipAsync(
            currentUser, tenantContext, membershipService, context.User);
        if (membership is null)
            return;

        if (Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) &&
            perms.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class TenantMemberHandler(
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantMembershipService membershipService)
    : AuthorizationHandler<TenantMemberRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMemberRequirement requirement)
    {
        var membership = await TenantAuthorization.GetActiveMembershipAsync(
            currentUser, tenantContext, membershipService, context.User);
        if (membership is not null)
            context.Succeed(requirement);
    }
}

public sealed class TenantOwnerHandler(
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ITenantMembershipService membershipService)
    : AuthorizationHandler<TenantOwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantOwnerRequirement requirement)
    {
        var membership = await TenantAuthorization.GetActiveMembershipAsync(
            currentUser, tenantContext, membershipService, context.User);
        if (membership?.RoleName == Permissions.TenantRoles.Owner)
            context.Succeed(requirement);
    }
}

public sealed class PlatformAdminOnlyHandler(UserManager<User> userManager)
    : AuthorizationHandler<PlatformAdminOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminOnlyRequirement requirement)
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

internal static class TenantAuthorization
{
    public static async Task<TenantMembershipInfo?> GetActiveMembershipAsync(
        ICurrentUserService currentUser,
        ITenantContext tenantContext,
        ITenantMembershipService membershipService,
        ClaimsPrincipal _)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return null;

        if (!tenantContext.HasTenant)
            return null;

        var membership = await membershipService.GetMembershipAsync(userId, tenantContext.GetRequiredTenantId());
        return membership is not null && membership.Status == (int)MembershipStatus.Active
            ? membership
            : null;
    }
}
