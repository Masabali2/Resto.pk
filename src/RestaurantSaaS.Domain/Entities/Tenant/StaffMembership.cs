using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Tenants;

/// <summary>
/// Links an Identity user to a tenant with a tenant-scoped role.
/// This is THE tenant membership record — ASP.NET UserRoles remain reserved
/// for global platform roles only (requirement #6).
///
/// Database invariant: UNIQUE(TenantId, UserId) — enforced by configuration.
/// </summary>
public class StaffMembership : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; private set; }

    /// <summary>Tenant-scoped role (Owner/Manager/Kitchen/Cashier/Waiter).</summary>
    public Guid RoleId { get; private set; }

    public MembershipStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }

    // Navigation properties
    public Tenant Tenant { get; private set; } = null!;
    public Identity.User User { get; private set; } = null!;
    public Identity.Role Role { get; private set; } = null!;

    /// <summary>
    /// Creates an ACTIVE membership. Invitations (Invited status) are handled
    /// by the invitation flow in a later phase.
    /// </summary>
    public static StaffMembership Create(Guid tenantId, Guid userId, Guid roleId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("MEMBERSHIP_TENANT_REQUIRED", "Membership requires a tenant.");

        if (userId == Guid.Empty)
            throw new DomainException("MEMBERSHIP_USER_REQUIRED", "Membership requires a user.");

        if (roleId == Guid.Empty)
            throw new DomainException("MEMBERSHIP_ROLE_REQUIRED", "Membership requires a role.");

        return new StaffMembership
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void ChangeRole(Guid newRoleId)
    {
        EnsureModifiable();

        if (newRoleId == Guid.Empty)
            throw new DomainException("MEMBERSHIP_ROLE_REQUIRED", "Membership requires a role.");

        RoleId = newRoleId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        EnsureModifiable();
        Status = MembershipStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Remove()
    {
        if (Status is MembershipStatus.Removed)
            throw new DomainException("MEMBERSHIP_ALREADY_REMOVED", "Membership is already removed.");

        Status = MembershipStatus.Removed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (Status == MembershipStatus.Removed)
            throw new DomainException("MEMBERSHIP_REMOVED", "A removed membership cannot be reactivated; create a new one.");

        Status = MembershipStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActiveInTenant(Guid tenantId) =>
        Status == MembershipStatus.Active && TenantId == tenantId;

    private void EnsureModifiable()
    {
        if (Status == MembershipStatus.Removed)
            throw new DomainException("MEMBERSHIP_REMOVED", "A removed membership cannot be modified.");
    }
}
