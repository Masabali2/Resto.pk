using Microsoft.AspNetCore.Identity;

namespace RestaurantSaaS.Domain.Entities.Identity;

/// <summary>
/// ASP.NET Core Identity role — used ONLY for GLOBAL PLATFORM roles
/// (e.g. PlatformAdmin). Tenant-level roles (Owner/Manager/Kitchen/Cashier/
/// Waiter) are NOT Identity roles; they are modelled by StaffMembership.RoleId
/// so that the same Role row can be reused across tenants without creating
/// per-tenant Identity roles (requirement #6: one authorization system, two scopes).
/// </summary>
public class Role : IdentityRole<Guid>
{
    /// <summary>True when the role grants platform-wide authority (not tenant-scoped).</summary>
    public bool IsPlatformRole { get; set; }

    /// <summary>Human-readable description used by admin UIs.</summary>
    public string? Description { get; set; }
}
