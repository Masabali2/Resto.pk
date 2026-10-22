using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Infrastructure;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Guards the dependency injection registrations of the subscription stack.
/// A missing registration only fails at runtime, when a controller asks for
/// the service — these tests make that failure visible at build time.
/// </summary>
public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=RestoPkDependencyTest;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddInfrastructure_ShouldRegisterTheSubscriptionServices()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<ITenantSubscriptionService>()
            .Should()
            .BeOfType<TenantSubscriptionService>();

        scope.ServiceProvider
            .GetRequiredService<ISubscriptionPlanService>()
            .Should()
            .BeOfType<SubscriptionPlanService>();
    }

    [Fact]
    public void AddInfrastructure_ShouldRegisterTheAccessGuardAndTheTrialGate()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<TenantAccessGuard>()
            .Should()
            .NotBeNull();

        // The trial gate guards a process wide invariant, so it must be a
        // singleton: two requests of the same process share it.
        scope.ServiceProvider
            .GetRequiredService<TenantSubscriptionGate>()
            .Should()
            .BeSameAs(provider.GetRequiredService<TenantSubscriptionGate>());
    }
}
