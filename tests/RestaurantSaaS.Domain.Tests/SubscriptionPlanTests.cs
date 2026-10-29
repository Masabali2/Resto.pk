using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Tests;

public sealed class SubscriptionPlanTests
{
    [Fact]
    public void Create_ShouldStorePkrPriceAndYearlyInterval()
    {
        var plan = SubscriptionPlan.Create(
            "starter",
            "Starter",
            Money.New(999m, "PKR"),
            BillingInterval.Yearly,
            1,
            10,
            100,
            "Starter plan.");

        plan.Code.Should().Be("starter");
        plan.Name.Should().Be("Starter");
        plan.GetPrice().Should().Be(Money.New(999m, "PKR"));
        plan.BillingInterval.Should().Be(BillingInterval.Yearly);
        plan.Currency.Should().Be(Money.DefaultCurrency);
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRequireCode()
    {
        var act = () => SubscriptionPlan.Create(
            "",
            "Starter",
            Money.New(999m, "PKR"),
            BillingInterval.Monthly,
            1,
            10,
            100);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("PLAN_CODE_REQUIRED");
    }

    [Fact]
    public void Create_ShouldRejectNegativePrice()
    {
        var act = () => SubscriptionPlan.Create(
            "starter",
            "Starter",
            Money.New(-1m, "PKR"),
            BillingInterval.Monthly,
            1,
            10,
            100);

        act.Should().Throw<DomainException>();
    }
}
