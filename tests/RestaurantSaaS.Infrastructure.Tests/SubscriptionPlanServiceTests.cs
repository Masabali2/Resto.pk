using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Behaviour of the plan catalogue service: active-only public listing and
/// plan creation with PKR pricing and billing intervals.
/// </summary>
public sealed class SubscriptionPlanServiceTests
{
    [Fact]
    public async Task ListAsync_ShouldReturnOnlyActivePlans()
    {
        var fx = TestFactory.Create();

        var active = SubscriptionPlan.Create(
            "starter",
            "Starter",
            Money.New(999m, "PKR"),
            BillingInterval.Monthly,
            1, 10, 100,
            "For new restaurants.");
        var inactive = SubscriptionPlan.Create(
            "starter",
            "Starter Yearly",
            Money.New(9999m, "PKR"),
            BillingInterval.Yearly,
            1, 10, 100,
            "For new restaurants.");
        inactive.SetActive(false);

        fx.Db.SubscriptionPlans.AddRange(active, inactive);
        await fx.Db.SaveChangesAsync();

        var service = new SubscriptionPlanService(fx.Db);

        var page = await service.ListAsync(onlyActive: true, 1, 10, CancellationToken.None);

        page.Items.Should().HaveCount(1);
        page.Items[0].Code.Should().Be("starter");
        page.Items[0].IsActive.Should().BeTrue();

        var all = await service.ListAsync(onlyActive: false, 1, 10, CancellationToken.None);
        all.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistCodePriceCurrencyAndInterval()
    {
        var fx = TestFactory.Create();
        var service = new SubscriptionPlanService(fx.Db);

        var result = await service.CreateAsync(
            new CreateSubscriptionPlanRequest(
                "professional",
                "Professional",
                "For growing restaurants.",
                2999m,
                "PKR",
                "Yearly",
                3,
                50,
                500),
            CancellationToken.None);

        result.Code.Should().Be("professional");
        result.Name.Should().Be("Professional");
        result.Price.Should().Be(2999m);
        result.Currency.Should().Be("PKR");
        result.BillingInterval.Should().Be("Yearly");
        result.IsActive.Should().BeTrue();

        var stored = await fx.Db.SubscriptionPlans
            .AsNoTracking()
            .SingleAsync(p => p.Code == "professional");

        stored.Price.Should().Be(2999m);
        stored.Currency.Should().Be("PKR");
        stored.BillingInterval.Should().Be(BillingInterval.Yearly);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectDuplicateCode()
    {
        var fx = TestFactory.Create();
        var service = new SubscriptionPlanService(fx.Db);

        await service.CreateAsync(
            new CreateSubscriptionPlanRequest(
                "starter", "Starter", null, 999m, "PKR", "Monthly", 1, 10, 100),
            CancellationToken.None);

        var act = () => service.CreateAsync(
            new CreateSubscriptionPlanRequest(
                "starter", "Starter Copy", null, 1999m, "PKR", "Monthly", 1, 10, 100),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
