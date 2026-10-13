
using Microsoft.AspNetCore.Authorization;

namespace RestaurantSaaS.Infrastructure.Authorization;

/// <summary>
/// Base type for tenant-scoped authorization requirements.
/// Requirements contain authorization data only.
/// Dependencies belong in authorization handlers.
/// </summary>
public abstract class TenantRequirementBase : IAuthorizationRequirement
{
}

/// <summary>
/// Requires an active membership in the current tenant
/// and a tenant role that grants the requested permission.
/// </summary>
public sealed class TenantPermissionRequirement(string permission)
    : TenantRequirementBase
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Requires an active membership in the current tenant.
/// </summary>
public sealed class TenantMemberRequirement : TenantRequirementBase
{
}

/// <summary>
/// Requires an active Owner membership in the current tenant.
/// </summary>
public sealed class TenantOwnerRequirement : TenantRequirementBase
{
}

/// <summary>
/// Requires the global PlatformAdmin Identity role.
/// This role is independent of tenant membership.
/// </summary>
public sealed class PlatformAdminOnlyRequirement
    : IAuthorizationRequirement
{
}