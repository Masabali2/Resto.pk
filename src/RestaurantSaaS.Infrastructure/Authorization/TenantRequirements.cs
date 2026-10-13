using Microsoft.AspNetCore.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Authorization;

/// <summary>
/// Base for tenant-scoped requirements. Requirements are plain data carriers —
/// services are injected into the HANDLERS (standard ASP.NET Core pattern),
/// never onto the requirement itself.
/// </summary>
public abstract class TenantRequirementBase : IAuthorizationRequirement;

/// <summary>
/// Tenant-aware authorization requirement: the acting user must hold an ACTIVE
/// StaffMembership in the CURRENT resolved tenant whose tenant role grants the
/// given permission. Resolved live from the database — stale JWT role claims are
/// never consulted (Phase-2 rule #13).
/// </summary>
public sealed class TenantPermissionRequirement(string permission) : TenantRequirementBase
{
    public string Permission { get; } = permission;
}

/// <summary>Requires an active membership in the current tenant (any role).</summary>
public sealed class TenantMemberRequirement : TenantRequirementBase;

/// <summary>Requires the current user to be an ACTIVE Owner of the current tenant.</summary>
public sealed class TenantOwnerRequirement : TenantRequirementBase;

/// <summary>
/// Platform-scope requirement: the caller must hold the GLOBAL Identity role
/// PlatformAdmin (AspNetUserRoles), validated live against the Identity store.
/// Deliberately separate from tenant requirements — platform administration is
/// not tenant membership (Phase-2 rule #12).
/// </summary>
public sealed class PlatformAdminOnlyRequirement : IAuthorizationRequirement;
