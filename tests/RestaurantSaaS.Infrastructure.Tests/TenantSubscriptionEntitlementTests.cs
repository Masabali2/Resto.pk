
using FluentAssertions;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Guards the subscription/entitlement boundary of a tenant-scoped service.
/// </summary>
public sealed class TenantSubscriptionEntitlementTests
{
    [Fact]
    public async Task EnsurePermissionAsync_ShouldAllowTenantWithValidTrial()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Trial Tenant", $"trial-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenant.Id,
            Guid.NewGuid(),
            now.AddDays(-1),
            now.AddDays(29)));

        await fx.Db.SaveChangesAsync();

        var result = await fx.AccessGuard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        result.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldKeepReadsAvailableAfterTrialExpiry()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Expired Trial Tenant", $"expired-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenant.Id,
            Guid.NewGuid(),
            now.AddDays(-31),
            now.AddDays(-1)));

        await fx.Db.SaveChangesAsync();

        // An expired trial must not blanket-disable the workspace: reads and
        // subscription management stay available so the owner can reach the
        // upgrade experience.
        var result = await fx.AccessGuard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        result.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task EnsureSubscriptionEntitledAsync_ShouldRejectTenantWithExpiredTrial()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Expired Trial Tenant", $"expired-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenant.Id,
            Guid.NewGuid(),
            now.AddDays(-31),
            now.AddDays(-1)));

        await fx.Db.SaveChangesAsync();

        var act = () => fx.AccessGuard.EnsureSubscriptionEntitledAsync(
            tenant.Id,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();

        exception.Which.Code.Should().Be("SUBSCRIPTION_NOT_ENTITLED");
    }

    [Fact]
    public async Task EnsureSubscriptionEntitledAsync_ShouldRejectTenantWithoutSubscription()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "No Subscription Tenant", $"no-sub-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var act = () => fx.AccessGuard.EnsureSubscriptionEntitledAsync(
            tenant.Id,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();

        exception.Which.Code.Should().Be("SUBSCRIPTION_REQUIRED");
    }

    [Fact]
    public async Task EnsureSubscriptionEntitledAsync_ShouldRejectAnotherWorkspace()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Owner Tenant", $"owner-{Guid.NewGuid():N}");

        var otherTenant = Tenant.Create(
            "Other Tenant",
            $"other-{Guid.NewGuid():N}");
        otherTenant.ChangeStatus(TenantStatus.Active);

        fx.Db.Tenants.Add(otherTenant);
        await fx.Db.SaveChangesAsync();

        fx.TenantContext.SetTenant(tenant.Id);

        var act = () => fx.AccessGuard.EnsureSubscriptionEntitledAsync(
            otherTenant.Id,
            CancellationToken.None);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldRejectUserWithoutMembershipInTenant()
    {
        var fx = TestFactory.Create();
        var (tenantA, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Tenant A", $"tenant-a-{Guid.NewGuid():N}");

        var tenantB = Tenant.Create(
            "Tenant B",
            $"tenant-b-{Guid.NewGuid():N}");

        tenantB.ChangeStatus(TenantStatus.Active);

        fx.Db.Tenants.Add(tenantB);
        await fx.Db.SaveChangesAsync();

        fx.TenantContext.SetTenant(tenantB.Id);

        var act = () => fx.AccessGuard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<Exception>();

        tenantA.Id.Should().NotBe(tenantB.Id);
    }

    [Fact]
    public async Task EnsureTenantMemberAsync_ShouldAllowSubscriptionManagementAfterTrialExpiry()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Expired Management Tenant", $"manage-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenant.Id,
            Guid.NewGuid(),
            now.AddDays(-31),
            now.AddDays(-1)));

        await fx.Db.SaveChangesAsync();

        var result = await fx.AccessGuard.EnsureTenantMemberAsync(
            Permissions.TenantView,
            CancellationToken.None);

        result.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task EnsureTenantMemberAsync_ShouldAllowPendingWorkspaceToManageItsSubscription()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Pending Tenant", $"pending-{Guid.NewGuid():N}",
            TenantStatus.Pending);

        fx.TenantContext.SetTenant(tenant.Id);

        var result = await fx.AccessGuard.EnsureTenantMemberAsync(
            Permissions.TenantView,
            CancellationToken.None);

        result.Should().Be(tenant.Id);
    }
}
