using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Seed;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// The development seeder is the single source of truth for the free-trial
/// plan and the approved paid plan catalogue. Paid plans are seeded only
/// because their PKR prices were explicitly approved.
/// </summary>
public sealed class SubscriptionPlanSeedingTests
{
    [Fact]
    public async Task SeedAsync_ShouldSeedTheApprovedCatalogueExactlyOnce()
    {
        var fx = TestFactory.Create();

        await DbSeeder.SeedAsync(fx.Db, new DateTimeProvider());
        await DbSeeder.SeedAsync(fx.Db, new DateTimeProvider());

        var plans = await fx.Db.SubscriptionPlans
            .AsNoTracking()
            .ToListAsync();

        // One free trial + two billing variants for each of three paid plans.
        plans.Should().HaveCount(7);

        var freeTrial = plans.Single(p => p.Code == "free-trial");
        freeTrial.Name.Should().Be(SubscriptionTrialPolicy.TrialPlanName);
        freeTrial.Price.Should().Be(0m);
        freeTrial.Currency.Should().Be(Money.DefaultCurrency);
        freeTrial.BillingInterval.Should().Be(BillingInterval.Monthly);
        freeTrial.IsActive.Should().BeTrue();

        var starterMonthly = plans.Single(p =>
            p.Code == "starter" && p.BillingInterval == BillingInterval.Monthly);
        starterMonthly.Price.Should().Be(1499m);
        starterMonthly.Currency.Should().Be("PKR");

        var starterYearly = plans.Single(p =>
            p.Code == "starter" && p.BillingInterval == BillingInterval.Yearly);
        starterYearly.Price.Should().Be(14990m);
        starterYearly.Currency.Should().Be("PKR");

        var professionalMonthly = plans.Single(p =>
            p.Code == "professional" && p.BillingInterval == BillingInterval.Monthly);
        professionalMonthly.Price.Should().Be(3999m);
        professionalMonthly.Currency.Should().Be("PKR");

        var professionalYearly = plans.Single(p =>
            p.Code == "professional" && p.BillingInterval == BillingInterval.Yearly);
        professionalYearly.Price.Should().Be(39990m);
        professionalYearly.Currency.Should().Be("PKR");

        var businessMonthly = plans.Single(p =>
            p.Code == "business" && p.BillingInterval == BillingInterval.Monthly);
        businessMonthly.Price.Should().Be(7999m);
        businessMonthly.Currency.Should().Be("PKR");

        var businessYearly = plans.Single(p =>
            p.Code == "business" && p.BillingInterval == BillingInterval.Yearly);
        businessYearly.Price.Should().Be(79990m);
        businessYearly.Currency.Should().Be("PKR");

        // Idempotent: a second run does not duplicate or overwrite anything.
        var afterSecondRun = await fx.Db.SubscriptionPlans.AsNoTracking().CountAsync();
        afterSecondRun.Should().Be(7);
    }

    [Fact]
    public async Task SeedAsync_ShouldNotOverwriteAnExistingPlan()
    {
        var fx = TestFactory.Create();

        var existing = SubscriptionPlan.Create(
            "starter",
            "Starter",
            Money.New(1m, "PKR"),
            BillingInterval.Monthly,
            1,
            1,
            1,
            "Administrator-managed plan.");

        fx.Db.SubscriptionPlans.Add(existing);
        await fx.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(fx.Db, new DateTimeProvider());

        var plan = await fx.Db.SubscriptionPlans
            .AsNoTracking()
            .SingleAsync(p => p.Code == "starter" && p.BillingInterval == BillingInterval.Monthly);

        plan.Price.Should().Be(1m);
    }
}
