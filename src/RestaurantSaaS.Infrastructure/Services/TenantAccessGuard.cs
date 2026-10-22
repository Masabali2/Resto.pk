using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Single entry point for tenant-scoped authorization.
///
/// Three distinct guarantees are exposed, because a workspace has three
/// different states in which it may legitimately call the API:
///
///  1. <see cref="EnsureTenantMemberAsync"/> — the caller is an active member
///     of the active workspace and its role grants the permission. This holds
///     for pending, suspended and lapsed workspaces, so subscription management
///     and the upgrade experience stay reachable.
///  2. <see cref="EnsurePermissionAsync"/> — the same, plus the workspace is
///     operationally active. Used by ordinary workspace features.
///  3. <see cref="EnsureSubscriptionEntitledAsync"/> — the workspace holds a
///     valid (non-expired) subscription. Used by subscription-protected
///     operations.
/// </summary>
public sealed class TenantAccessGuard(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService)
{
    /// <summary>
    /// Resolves the acting workspace for an operation that requires an
    /// operationally active workspace: active membership, role permission and
    /// an active tenant.
    /// </summary>
    public async Task<Guid> EnsurePermissionAsync(
        string requiredPermission,
        CancellationToken ct)
    {
        var tenantId = await EnsureTenantMemberAsync(requiredPermission, ct);

        var tenantOperational = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);

        if (!tenantOperational)
            throw new DomainException("TENANT_NOT_ACTIVE", "This tenant is not active.");

        return tenantId;
    }

    /// <summary>
    /// Resolves the acting workspace for operations that must stay available
    /// while the workspace is pending, suspended, or its subscription has
    /// lapsed. Only membership and the role permission are verified — the
    /// workspace status and the subscription are deliberately NOT checked.
    /// </summary>
    public async Task<Guid> EnsureTenantMemberAsync(
        string requiredPermission,
        CancellationToken ct)
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

        return tenantId;
    }

    /// <summary>
    /// Ensures the workspace holds a subscription that is valid right now.
    ///
    /// The check is evaluated against the persisted subscription at the moment
    /// of the call — it never depends on a background job having marked the
    /// subscription as expired, and it never trusts a client supplied flag.
    /// </summary>
    public async Task EnsureSubscriptionEntitledAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (!tenantContext.HasTenant || tenantContext.GetRequiredTenantId() != tenantId)
            throw new CrossTenantAccessException();

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

    /// <summary>
    /// Resolves the acting workspace for a free-trial activation.
    ///
    /// Only the workspace owner may activate the trial: the owner is the role
    /// that carries the billing responsibility, and a trial must not be
    /// triggered by a delegated team member.
    /// </summary>
    public async Task<Guid> EnsureTrialOwnerAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();

        if (!tenantContext.HasTenant)
            throw new CrossTenantAccessException();

        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!string.Equals(
                membership.RoleName,
                Permissions.TenantRoles.Owner,
                StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                "Only the workspace owner can start the free trial.");
        }

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(Permissions.TenantUpdate))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        return tenantId;
    }
}
