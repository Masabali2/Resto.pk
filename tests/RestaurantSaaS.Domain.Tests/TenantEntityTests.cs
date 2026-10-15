using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

public class TenantEntityTests
{
    [Fact]
    public void Create_WhenValid_NormalizesSlugAndStartsPending()
    {
        var tenant = Tenant.Create("  Karachi Dhaba  ", "Karachi-Dhaba");

        tenant.Name.Should().Be("Karachi Dhaba");
        tenant.Status.Should().Be(TenantStatus.Pending);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameBlank_Throws(string name)
    {
        var act = () => Tenant.Create(name, "valid-slug");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TENANT_NAME_REQUIRED");
    }

    [Fact]
    public void Create_WhenNameTooLong_Throws()
    {
        var act = () => Tenant.Create(new string('n', 151), "valid-slug");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TENANT_NAME_TOO_LONG");
    }

    [Fact]
    public void Rename_WhenValid_UpdatesName()
    {
        var tenant = Tenant.Create("Old Name", "old-name");

        tenant.Rename("New Name");

        tenant.Name.Should().Be("New Name");
    }

    [Fact]
    public void ChangeStatus_CancelledIsTerminal()
    {
        var tenant = Tenant.Create("Dhaba", "dhaba");
        tenant.ChangeStatus(TenantStatus.Active);
        tenant.ChangeStatus(TenantStatus.Cancelled);

        var act = () => tenant.ChangeStatus(TenantStatus.Active);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TENANT_ALREADY_CANCELLED");
    }

    [Fact]
    public void EnsureOperational_WhenPending_Throws()
    {
        var tenant = Tenant.Create("Dhaba", "dhaba");

        var act = () => tenant.EnsureOperational();

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TENANT_NOT_ACTIVE");
    }

    [Fact]
    public void SoftDelete_PreventsFurtherMutation()
    {
        var tenant = Tenant.Create("Dhaba", "dhaba");
        tenant.SoftDelete();

        var act = () => tenant.Rename("Another");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TENANT_DELETED");
    }
}

public class StaffMembershipEntityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    [Fact]
    public void Create_WhenValid_StartsActiveWithJoinedAt()
    {
        var membership = StaffMembership.Create(TenantId, UserId, RoleId);

        membership.Status.Should().Be(MembershipStatus.Active);
        membership.TenantId.Should().Be(TenantId);
        membership.UserId.Should().Be(UserId);
        membership.RoleId.Should().Be(RoleId);
        membership.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WhenTenantMissing_Throws()
    {
        var act = () => StaffMembership.Create(Guid.Empty, UserId, RoleId);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "MEMBERSHIP_TENANT_REQUIRED");
    }

    [Fact]
    public void Create_WhenUserMissing_Throws()
    {
        var act = () => StaffMembership.Create(TenantId, Guid.Empty, RoleId);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "MEMBERSHIP_USER_REQUIRED");
    }

    [Fact]
    public void Create_WhenRoleMissing_Throws()
    {
        var act = () => StaffMembership.Create(TenantId, UserId, Guid.Empty);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "MEMBERSHIP_ROLE_REQUIRED");
    }

    [Fact]
    public void Suspend_MarksSuspended()
    {
        var membership = StaffMembership.Create(TenantId, UserId, RoleId);

        membership.Suspend();

        membership.Status.Should().Be(MembershipStatus.Suspended);
    }

    [Fact]
    public void Remove_IsTerminalForModification()
    {
        var membership = StaffMembership.Create(TenantId, UserId, RoleId);
        membership.Remove();

        var act = () => membership.ChangeRole(Guid.NewGuid());

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "MEMBERSHIP_REMOVED");
    }

    [Fact]
    public void Reactivate_FromRemoved_Throws()
    {
        var membership = StaffMembership.Create(TenantId, UserId, RoleId);
        membership.Remove();

        var act = () => membership.Reactivate();

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "MEMBERSHIP_REMOVED");
    }

    [Fact]
    public void IsActiveInTenant_ReflectsStatusAndTenant()
    {
        var membership = StaffMembership.Create(TenantId, UserId, RoleId);

        membership.IsActiveInTenant(TenantId).Should().BeTrue();
        membership.IsActiveInTenant(Guid.NewGuid()).Should().BeFalse();

        membership.Suspend();

        membership.IsActiveInTenant(TenantId).Should().BeFalse();
    }
}
