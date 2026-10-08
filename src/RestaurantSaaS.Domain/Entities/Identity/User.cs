using Microsoft.AspNetCore.Identity;

namespace RestaurantSaaS.Domain.Entities.Identity;

/// <summary>
/// Application user for ASP.NET Core Identity (staff/platform users only).
///
/// CRITICAL MULTI-TENANCY RULE (requirement #6/#39):
/// There is deliberately NO TenantId property here. A single user may hold
/// memberships in several tenants with different roles
/// (User A: Tenant A -> Manager, Tenant B -> Kitchen). Tenant scoping lives
/// exclusively in <see cref="Tenant.StaffMembership"/>.
///
/// Customers are NOT Identity users in V1 (see Domain/Entities/Customer).
/// </summary>
public class User : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Set by the audit interceptor; never by clients.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string FullName =>
        string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
            ? UserName ?? string.Empty
            : $"{FirstName} {LastName}".Trim();
}
