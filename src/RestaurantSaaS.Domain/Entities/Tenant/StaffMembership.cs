
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Tenants;

public class StaffMembership : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get;  set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    public MembershipStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public Identity.User User { get; private set; } = null!;
    public Identity.Role Role { get; private set; } = null!;

    private StaffMembership()
    {
    }

    public static StaffMembership Create(Guid tenantId, Guid userId, Guid roleId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException(
                "MEMBERSHIP_TENANT_REQUIRED",
                "Membership requires a tenant.");

        if (userId == Guid.Empty)
            throw new DomainException(
                "MEMBERSHIP_USER_REQUIRED",
                "Membership requires a user.");

        if (roleId == Guid.Empty)
            throw new DomainException(
                "MEMBERSHIP_ROLE_REQUIRED",
                "Membership requires a role.");

        var now = DateTime.UtcNow;

        return new StaffMembership
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            Status = MembershipStatus.Active,
            JoinedAt = now,
            CreatedAt = now
        };
    }

    public void ChangeRole(Guid newRoleId)
    {
        EnsureModifiable();

        if (newRoleId == Guid.Empty)
            throw new DomainException(
                "MEMBERSHIP_ROLE_REQUIRED",
                "Membership requires a role.");

        if (RoleId == newRoleId)
            return;

        RoleId = newRoleId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        EnsureModifiable();

        if (Status == MembershipStatus.Suspended)
            return;

        Status = MembershipStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Remove()
    {
        if (Status == MembershipStatus.Removed)
            throw new DomainException(
                "MEMBERSHIP_ALREADY_REMOVED",
                "Membership is already removed.");

        Status = MembershipStatus.Removed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (Status == MembershipStatus.Removed)
            throw new DomainException(
                "MEMBERSHIP_REMOVED",
                "A removed membership cannot be reactivated; create a new one.");

        if (Status == MembershipStatus.Active)
            return;

        if (Status != MembershipStatus.Suspended)
            throw new DomainException(
                "MEMBERSHIP_STATUS_INVALID",
                "Only suspended memberships can be reactivated.");

        Status = MembershipStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsActiveInTenant(Guid tenantId)
    {
        return Status == MembershipStatus.Active
            && TenantId == tenantId;
    }

    private void EnsureModifiable()
    {
        if (Status == MembershipStatus.Removed)
            throw new DomainException(
                "MEMBERSHIP_REMOVED",
                "A removed membership cannot be modified.");
    }
}