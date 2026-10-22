
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Behaviour of the workspace subscription: free-trial activation, trial
/// eligibility, entitlement enforcement and tenant isolation.
/// </summary>
public sealed class TenantSubscriptionServiceTests
{
    // ------------------------------------------------------------------
    // Scenario 1 + 2 + 3 — eligible owner starts a trial, it lasts exactly
    // 30 days and it activates the pending workspace.
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartTrialAsync_ShouldStartTrialAndActivatePendingWorkspace()
    {
        var (fx, tenant) = await CreatePendingWorkspaceAsync();

        var result = await fx.Subscriptions.StartTrialAsync(CancellationToken.None);

        result.Status.Should().Be(nameof(SubscriptionStatus.Trial));
        result.TenantId.Should().Be(tenant.Id);
        result.PlanName.Should().Be(SubscriptionTrialPolicy.TrialPlanName);
        result.IsEntitled.Should().BeTrue();
        result.AutoRenew.Should().BeFalse();

        // Scenario 2 — the trial lasts exactly 30 UTC days.
        (result.EndDate!.Value - result.StartDate).Should()
            .Be(TimeSpan.FromDays(SubscriptionTrialPolicy.TrialDurationDays));

        // Scenario 3 — the workspace is activated by the same operation.
        var reloaded = await fx.Db.Tenants
            .AsNoTracking()
            .FirstAsync(t => t.Id == tenant.Id);

        reloaded.Status.Should().Be(TenantStatus.Active);

        var subscription = await fx.Db.Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.TenantId == tenant.Id);

        subscription.Status.Should().Be(SubscriptionStatus.Trial);
        subscription.StartDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ------------------------------------------------------------------
    // Scenario 4 — the trial can only be consumed once.
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartTrialAsync_ShouldRejectASecondTrial()
    {
        var (fx, tenant) = await CreatePendingWorkspaceAsync();

        await fx.Subscriptions.StartTrialAsync(CancellationToken.None);

        Func<Task> act = () => fx.Subscriptions.StartTrialAsync(CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DomainException>();

        exception.Which.Message.Should().Contain("already been used");

        var subscriptions = await fx.Db.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenant.Id)
            .ToListAsync();

        subscriptions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTrialEligibilityAsync_ShouldReportConsumedTrial()
    {
        var (fx, _) = await CreatePendingWorkspaceAsync();

        await fx.Subscriptions.StartTrialAsync(CancellationToken.None);

        var eligibility = await fx.Subscriptions.GetTrialEligibilityAsync(CancellationToken.None);

        eligibility.Eligible.Should().BeFalse();
        eligibility.TrialUsed.Should().BeTrue();
        eligibility.TrialDurationDays.Should().Be(SubscriptionTrialPolicy.TrialDurationDays);
        eligibility.Reason.Should().NotBeNullOrWhiteSpace();
    }

    // ------------------------------------------------------------------
    // Scenario 5 — only the workspace owner may activate the trial.
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartTrialAsync_ShouldRejectNonOwnerMember()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Manager Workspace", $"manager-{Guid.NewGuid():N}",
            TenantStatus.Pending);

        await TestFactory.AddTrialPlanAsync(fx);

        fx.TenantContext.SetTenant(tenant.Id);

        // The account acts with a delegated Manager role.
        fx.Memberships.Memberships.Clear();
        TestFactory.AttachMembership(fx, tenant, "Manager");

        Func<Task> act = () => fx.Subscriptions.StartTrialAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ------------------------------------------------------------------
    // Scenario 6 — cross-tenant access is rejected.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetByTenantAsync_ShouldRejectAnotherWorkspace()
    {
        var (fx, _) = await CreatePendingWorkspaceAsync();

        var otherTenant = Tenant.Create(
            "Another Workspace",
            $"another-{Guid.NewGuid():N}");
        otherTenant.ChangeStatus(TenantStatus.Active);

        fx.Db.Tenants.Add(otherTenant);
        await fx.Db.SaveChangesAsync();

        Func<Task> act = () => fx.Subscriptions.GetByTenantAsync(otherTenant.Id, CancellationToken.None);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task GetMyStateAsync_ShouldRejectUserWithoutMembership()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Foreign Workspace", $"foreign-{Guid.NewGuid():N}",
            TenantStatus.Pending);

        fx.TenantContext.SetTenant(tenant.Id);
        fx.Memberships.Memberships.Clear();

        Func<Task> act = () => fx.Subscriptions.GetMyStateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    // ------------------------------------------------------------------
    // Scenario 7 — expired and missing subscriptions deny protected access.
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateRestaurant_ShouldRejectWorkspaceWithoutSubscription()
    {
        var (fx, _, _) = await CreateActiveWorkspaceAsync();

        Func<Task> act = () => fx.Restaurants.CreateAsync(NewRestaurantRequest());

        var exception = await act.Should().ThrowAsync<DomainException>();

        exception.Which.Code.Should().Be("SUBSCRIPTION_REQUIRED");
    }

    [Fact]
    public async Task CreateRestaurant_ShouldRejectWorkspaceWithExpiredTrial()
    {
        var (fx, tenantId, planId) = await CreateActiveWorkspaceAsync();

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenantId,
            planId,
            now.AddDays(-31),
            now.AddDays(-1)));

        await fx.Db.SaveChangesAsync();

        Func<Task> act = () => fx.Restaurants.CreateAsync(NewRestaurantRequest());

        var exception = await act.Should().ThrowAsync<DomainException>();

        exception.Which.Code.Should().Be("SUBSCRIPTION_NOT_ENTITLED");
    }

    // ------------------------------------------------------------------
    // Scenario 8 — a valid trial grants the protected operations.
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateRestaurant_ShouldSucceedWithValidTrial()
    {
        var (fx, tenantId, planId) = await CreateActiveWorkspaceAsync();

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenantId,
            planId,
            now.AddDays(-1),
            now.AddDays(29)));

        await fx.Db.SaveChangesAsync();

        var result = await fx.Restaurants.CreateAsync(NewRestaurantRequest());

        result.TenantId.Should().Be(tenantId);
        result.Name.Should().Be("Brewed & Co.");
    }

    // ------------------------------------------------------------------
    // Scenario 9 — subscription management stays available after expiry.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetMyStateAsync_ShouldRemainAvailableAfterTrialExpiry()
    {
        var (fx, tenantId, planId) = await CreateActiveWorkspaceAsync();

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenantId,
            planId,
            now.AddDays(-31),
            now.AddDays(-1)));

        await fx.Db.SaveChangesAsync();

        var state = await fx.Subscriptions.GetMyStateAsync(CancellationToken.None);

        state.HasSubscription.Should().BeTrue();
        state.Subscription!.Status.Should().Be(nameof(SubscriptionStatus.Trial));
        state.Subscription.IsEntitled.Should().BeFalse();
        state.TenantStatus.Should().Be(nameof(TenantStatus.Active));
        state.Trial.Eligible.Should().BeFalse();
        state.Trial.TrialUsed.Should().BeTrue();
        state.ServerTimeUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetMyStateAsync_ShouldRemainAvailableForPendingWorkspace()
    {
        var (fx, _) = await CreatePendingWorkspaceAsync();

        var state = await fx.Subscriptions.GetMyStateAsync(CancellationToken.None);

        state.HasSubscription.Should().BeFalse();
        state.Subscription.Should().BeNull();
        state.TenantStatus.Should().Be(nameof(TenantStatus.Pending));
        state.Trial.Eligible.Should().BeTrue();
        state.Trial.TrialUsed.Should().BeFalse();
        state.AvailablePlans.Should().NotBeEmpty();
        state.AvailablePlans
            .Should().Contain(p => p.Name == SubscriptionTrialPolicy.TrialPlanName);
    }

    // ------------------------------------------------------------------
    // Scenario 10 — concurrent trial requests cannot create duplicates.
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartTrialAsync_ShouldNotCreateDuplicateTrialsUnderConcurrency()
    {
        var databaseName = $"resto-concurrent-{Guid.NewGuid():N}";
        var gate = new TenantSubscriptionGate();

        var first = TestFactory.Create(databaseName: databaseName, gate: gate);
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            first, "Concurrent Workspace", $"concurrent-{Guid.NewGuid():N}",
            TenantStatus.Pending);

        await TestFactory.AddTrialPlanAsync(first);

        first.TenantContext.SetTenant(tenant.Id);

        // A second request acting on the same workspace, in another scope.
        var second = TestFactory.Create(
            resolvedTenantId: tenant.Id,
            databaseName: databaseName,
            gate: gate);

        TestFactory.AttachMembership(second, tenant);

        var results = await Task.WhenAll(
            CaptureAsync(() => first.Subscriptions.StartTrialAsync(CancellationToken.None)),
            CaptureAsync(() => second.Subscriptions.StartTrialAsync(CancellationToken.None)));

        results.Count(r => r.Succeeded).Should().Be(1);
        results.Count(r => !r.Succeeded).Should().Be(1);

        var subscriptions = await first.Db.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenant.Id)
            .ToListAsync();

        subscriptions.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // Scenario 11 — existing restaurant behaviour and isolation stay correct.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ListAsync_ShouldNotExposeRestaurantsOfAnotherWorkspace()
    {
        var (fx, tenantId, planId) = await CreateActiveWorkspaceAsync();

        var now = DateTime.UtcNow;
        fx.Db.Subscriptions.Add(Subscription.StartTrial(
            tenantId,
            planId,
            now.AddDays(-1),
            now.AddDays(29)));

        await fx.Db.SaveChangesAsync();

        var otherTenant = Tenant.Create(
            "Foreign Restaurant Workspace",
            $"foreign-restaurant-{Guid.NewGuid():N}");
        otherTenant.ChangeStatus(TenantStatus.Active);

        fx.Db.Tenants.Add(otherTenant);
        await fx.Db.SaveChangesAsync();

        await TestFactory.AddRestaurantAsync(fx, otherTenant.Id, "Foreign Restaurant");

        var page = await fx.Restaurants.ListAsync(1, 20);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private static async Task<(Fixture Fixture, Tenant Tenant)> CreatePendingWorkspaceAsync()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Pending Workspace", $"pending-{Guid.NewGuid():N}",
            TenantStatus.Pending);

        await TestFactory.AddTrialPlanAsync(fx);

        fx.TenantContext.SetTenant(tenant.Id);

        return (fx, tenant);
    }

    private static async Task<(Fixture Fixture, Guid TenantId, Guid PlanId)> CreateActiveWorkspaceAsync()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx, "Active Workspace", $"active-{Guid.NewGuid():N}",
            TenantStatus.Active);

        var plan = await TestFactory.AddTrialPlanAsync(fx);

        fx.TenantContext.SetTenant(tenant.Id);

        return (fx, tenant.Id, plan.Id);
    }

    private static CreateRestaurantRequest NewRestaurantRequest() =>
        new(
            "Brewed & Co.",
            Description: null,
            Phone: null,
            Email: null,
            Address: new AddressInput(
                "1 Main Street",
                null,
                "Lahore",
                "Pakistan",
                null),
            // "UTC" is the only identifier that resolves on every platform;
            // the time zone is irrelevant for the subscription assertions.
            TimeZone: "UTC");

    private sealed record Attempt(bool Succeeded, Exception? Error);

    private static async Task<Attempt> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();

            return new Attempt(true, null);
        }
        catch (Exception ex)
        {
            return new Attempt(false, ex);
        }
    }
}
