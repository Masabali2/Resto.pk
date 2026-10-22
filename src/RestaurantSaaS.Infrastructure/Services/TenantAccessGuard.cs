using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class TenantAccessGuard(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService)
{
    public async Task<Guid> EnsurePermissionAsync(string requiredPermission, CancellationToken ct , bool requireEntitlement = true)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();

        if (!tenantContext.HasTenant)
            throw new CrossTenantAccessException();

        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(requiredPermission))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        var tenantOperational = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);
        if (!tenantOperational)
            throw new DomainException("TENANT_NOT_ACTIVE", "This tenant is not active.");
        if(requireEntitlement)
            await EnsureSubscriptionEntitledAsync(tenantId, ct);
        return tenantId;
    }
    public async Task EnsureSubscriptionEntitledAsync(
    Guid tenantId,
    CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var subscription = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Status,
                s.StartDate,
                s.EndDate
            })
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            throw new DomainException(
                "SUBSCRIPTION_REQUIRED",
                "An active trial or subscription is required to use this feature.");
        }

        var entitled =
            subscription.Status is SubscriptionStatus.Trial
                or SubscriptionStatus.Active
                or SubscriptionStatus.PastDue
            && subscription.StartDate <= now
            && (subscription.EndDate is null || subscription.EndDate > now);

        if (!entitled)
        {
            throw new DomainException(
                "SUBSCRIPTION_NOT_ENTITLED",
                "Your trial or subscription has ended. Select an available plan to restore access.");
        }
    }
    public async Task<Guid> EnsureTrialOnboardingAccessAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new NotAuthenticatedException();

        if (!tenantContext.HasTenant)
            throw new CrossTenantAccessException();

        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(
            userId,
            tenantId,
            ct);

        if (membership is null ||
            membership.Status != (int)MembershipStatus.Active)
        {
            throw new CrossTenantAccessException();
        }

        if (!string.Equals(
                membership.RoleName,
                "Owner",
                StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                "Only the tenant owner can start the trial.");
        }

        var tenantExists = await db.Tenants
            .AsNoTracking()
            .AnyAsync(
                t => t.Id == tenantId &&
                     t.Status == TenantStatus.Pending &&
                     !t.IsDeleted,
                ct);

        if (!tenantExists)
        {
            throw new DomainException(
                "TENANT_NOT_ELIGIBLE_FOR_TRIAL",
                "Only a pending tenant can start its initial trial.");
        }

        return tenantId;
    }
}
