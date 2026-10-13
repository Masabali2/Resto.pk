using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Authorization;

/// <summary>
/// Tenant-aware authorization requirement: the acting user must hold an ACTIVE
/// StaffMembership in the CURRENT resolved tenant whose tenant role grants the
/// given permission. Resolved live from the database — stale JWT role claims are
/// never consulted (Phase-2 rule #13).
/// </summary>
public sealed class TenantPermissionRequirement(string permission)
    : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Requires an active membership in the current tenant (any role).</summary>
public sealed class TenantMemberRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement;

/// <summary>Requires the current user to be an ACTIVE Owner of the current tenant.</summary>
public sealed class TenantOwnerRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement;
