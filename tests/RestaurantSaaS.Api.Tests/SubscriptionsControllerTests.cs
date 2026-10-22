using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Contract of the subscription endpoints: which service call they make and
/// which envelope they return.
/// </summary>
public sealed class SubscriptionsControllerTests
{
    private readonly Mock<ITenantSubscriptionService> _subscriptions = new();
    private readonly SubscriptionsController _controller;

    public SubscriptionsControllerTests()
    {
        _controller = new SubscriptionsController(_subscriptions.Object);
    }

    private static CurrentSubscriptionStateDto State() => new(
        Guid.NewGuid(),
        "Resto One",
        "Pending",
        false,
        null,
        new TrialEligibilityDto(true, false, 30, null),
        Array.Empty<SubscriptionPlanDto>(),
        DateTime.UtcNow);

    [Fact]
    public async Task GetMyState_ReturnsTheWorkspaceState()
    {
        var state = State();

        _subscriptions
            .Setup(x => x.GetMyStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var result = await _controller.GetMyState(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        var envelope = ok.Value.Should().BeOfType<ApiResponse<CurrentSubscriptionStateDto>>().Subject;

        envelope.Success.Should().BeTrue();
        envelope.Data.Should().BeSameAs(state);
    }

    [Fact]
    public async Task GetTrialEligibility_ReturnsTheEligibility()
    {
        var eligibility = new TrialEligibilityDto(false, true, 30, "Already used.");

        _subscriptions
            .Setup(x => x.GetTrialEligibilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibility);

        var result = await _controller.GetTrialEligibility(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        var envelope = ok.Value.Should().BeOfType<ApiResponse<TrialEligibilityDto>>().Subject;

        envelope.Data.Should().BeSameAs(eligibility);
    }

    [Fact]
    public async Task StartTrial_ReturnsTheCreatedSubscription()
    {
        var subscription = new SubscriptionDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Free Trial",
            "Trial",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            false,
            true,
            DateTime.UtcNow,
            null);

        _subscriptions
            .Setup(x => x.StartTrialAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var result = await _controller.StartTrial(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        var envelope = ok.Value.Should().BeOfType<ApiResponse<SubscriptionDto>>().Subject;

        envelope.Data.Should().BeSameAs(subscription);
    }

    [Fact]
    public async Task ListPlans_ReturnsTheAvailablePlans()
    {
        var plans = new List<SubscriptionPlanDto>
        {
            new(
                Guid.NewGuid(),
                "Free Trial",
                "30-day free trial.",
                0m,
                "PKR",
                "Monthly",
                5,
                100,
                500,
                true,
                DateTime.UtcNow,
                null)
        };

        _subscriptions
            .Setup(x => x.ListAvailablePlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var result = await _controller.ListPlans(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        var envelope = ok.Value
            .Should()
            .BeOfType<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>()
            .Subject;

        envelope.Data.Should().HaveCount(1);
        envelope.Data![0].Name.Should().Be("Free Trial");
    }
}
