
using FluentAssertions;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

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

        var guard = CreateGuard(fx);

        var result = await guard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        result.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldRejectTenantWithExpiredTrial()
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

        var guard = CreateGuard(fx);

        var act = () => guard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldRejectTenantWithoutSubscription()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "No Subscription Tenant", $"no-sub-{Guid.NewGuid():N}");

        fx.TenantContext.SetTenant(tenant.Id);

        var guard = CreateGuard(fx);

        var act = () => guard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<DomainException>();
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

        var guard = CreateGuard(fx);

        var act = () => guard.EnsurePermissionAsync(
            Permissions.RestaurantView,
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<Exception>();

        tenantA.Id.Should().NotBe(tenantB.Id);
    }

    [Fact]
    public async Task EnsurePermissionAsync_ShouldAllowSubscriptionManagementAfterTrialExpiry()
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

        var guard = CreateGuard(fx);

        var result = await guard.EnsurePermissionAsync(
            Permissions.TenantView,
            CancellationToken.None,
            requireEntitlement: false);

        result.Should().Be(tenant.Id);
    }

    private static TenantAccessGuard CreateGuard(Fixture fx) =>
        new(
            fx.Db,
            fx.TenantContext,
            fx.CurrentUser.Object,
            fx.Memberships);
}
