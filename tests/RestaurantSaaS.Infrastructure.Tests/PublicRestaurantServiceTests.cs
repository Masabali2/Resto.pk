using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class PublicRestaurantServiceTests
{
    private static ApplicationDbContext CreateInMemoryDb(out Guid tenantId, out Guid restaurantId, out Guid websiteId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        // Create tenant first so we know its ID for the ITenantContext mock.
        var tenant = Tenant.Create("Test Tenant", "test-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        tenantId = tenant.Id;

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.HasTenant).Returns(true);
        tenantContext.Setup(t => t.TenantId).Returns(tenantId);

        var db = new ApplicationDbContext(options, tenantContext.Object, dateTimeProvider.Object);
        db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(
            tenant.Id,
            "Test Restaurant",
            address,
            taxRate: 16m);
        restaurantId = restaurant.Id;
        restaurant.ChangeStatus(RestaurantStatus.Active);
        db.Restaurants.Add(restaurant);

        // Create template first so we know its ID before creating the website.
        var template = WebsiteTemplate.Create("Test Template", "test-template");
        var website = Website.Create(
            tenant.Id,
            restaurant.Id,
            template.Id,
            "Test Site",
            "test-site");
        websiteId = website.Id;
        var config = WebsiteConfiguration.CreateDraft(website.Id, "{}", 1);
        website.Publish(config, template, DateTime.UtcNow);
        db.Websites.Add(website);

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_ValidSlug_ReturnsRestaurant()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out var websiteId);
        var service = new PublicRestaurantService(db);

        // Subscription entitlement is required for public ordering.
        var now = DateTime.UtcNow;
        db.Subscriptions.Add(Subscription.Start(
            tenantId,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            now.AddDays(-1),
            now.AddDays(30)));
        db.SaveChanges();

        var result = await service.GetValidRestaurantForOrderingAsync("test-site");

        result.Should().NotBeNull();
        result!.Id.Should().Be(restaurantId);
        result.TenantId.Should().Be(tenantId);
        result.Name.Should().Be("Test Restaurant");
        result.TaxRate.Should().Be(16m);
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_UnknownSlug_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out _, out _, out _);
        var service = new PublicRestaurantService(db);

        var result = await service.GetValidRestaurantForOrderingAsync("nonexistent-site");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_UnpublishedWebsite_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out var tenantId, out var restaurantId, out _);
        var service = new PublicRestaurantService(db);

        var unpublished = Website.Create(
            tenantId,
            restaurantId,
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "Unpublished Site",
            "unpublished-site");
        db.Websites.Add(unpublished);
        db.SaveChanges();

        var result = await service.GetValidRestaurantForOrderingAsync("unpublished-site");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_InactiveRestaurant_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out _, out var restaurantId, out var websiteId);
        var service = new PublicRestaurantService(db);

        var restaurant = db.Restaurants.First(r => r.Id == restaurantId);
        restaurant.ChangeStatus(RestaurantStatus.TemporarilyClosed);
        db.SaveChanges();

        var result = await service.GetValidRestaurantForOrderingAsync("test-site");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_InactiveTenant_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out var tenantId, out _, out _);
        var service = new PublicRestaurantService(db);

        var tenant = db.Tenants.First();
        tenant.ChangeStatus(TenantStatus.Suspended);
        db.SaveChanges();

        var result = await service.GetValidRestaurantForOrderingAsync("test-site");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_EmptySlug_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out _, out _, out _);
        var service = new PublicRestaurantService(db);

        var result = await service.GetValidRestaurantForOrderingAsync("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_NoSubscription_ReturnsNull()
    {
        using var db = CreateInMemoryDb(out _, out _, out var websiteId);
        var service = new PublicRestaurantService(db);

        var result = await service.GetValidRestaurantForOrderingAsync("test-site");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetValidRestaurantForOrderingAsync_WithValidSubscription_ReturnsRestaurant()
    {
        using var db = CreateInMemoryDb(out var tenantId, out _, out var websiteId);
        var service = new PublicRestaurantService(db);

        var now = DateTime.UtcNow;
        db.Subscriptions.Add(Subscription.Start(
            tenantId,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            now.AddDays(-1),
            now.AddDays(30)));
        db.SaveChanges();

        var result = await service.GetValidRestaurantForOrderingAsync("test-site");

        result.Should().NotBeNull();
    }
}
