using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Infrastructure.Tests;

internal sealed record Fixture(
    ApplicationDbContext Db,
    TenantContext TenantContext,
    Mock<ICurrentUserService> CurrentUser,
    StubMembershipService Memberships,
    RestaurantService Restaurants)
{
    public void Unauthenticated() =>
        CurrentUser.Setup(x => x.IsAuthenticated).Returns(false);
}

internal sealed class StubMembershipService : ITenantMembershipService
{
    public List<TenantMembershipInfo> Memberships { get; } = [];

    private readonly Dictionary<Guid, Guid> _membershipUsers = [];

    public void AddMembership(Guid membershipId, Guid userId) =>
        _membershipUsers[membershipId] = userId;

    public Task<TenantMembershipInfo?> GetMembershipAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Memberships.FirstOrDefault(m =>
            _membershipUsers.TryGetValue(m.MembershipId, out var owner) &&
            owner == userId && m.TenantId == tenantId));

    public Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantMembershipInfo>>(
            Memberships.Where(m =>
                _membershipUsers.TryGetValue(m.MembershipId, out var owner) &&
                owner == userId).ToList());
}

internal static class TestFactory
{
    public static readonly Guid UserId = Guid.NewGuid();
    public static readonly Guid OwnerRoleId = Guid.NewGuid();
    public static readonly Guid ManagerRoleId = Guid.NewGuid();
    public static readonly Guid KitchenRoleId = Guid.NewGuid();

    public static Fixture Create(Guid? resolvedTenantId = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"resto-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var tenantContext = new TenantContext();
        if (resolvedTenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var db = new ApplicationDbContext(options, tenantContext, new DateTimeProvider());

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.IsAuthenticated).Returns(true);
        currentUser.Setup(x => x.UserId).Returns(UserId);

        var memberships = new StubMembershipService();

        var service = new RestaurantService(
            db,
            tenantContext,
            currentUser.Object,
            memberships);

        return new Fixture(db, tenantContext, currentUser, memberships, service);
    }

    public static async Task SeedRolesAsync(ApplicationDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = OwnerRoleId, Name = "Owner", NormalizedName = "OWNER" },
            new Role { Id = ManagerRoleId, Name = "Manager", NormalizedName = "MANAGER" },
            new Role { Id = KitchenRoleId, Name = "Kitchen", NormalizedName = "KITCHEN" });

        await db.SaveChangesAsync();
    }

    public static async Task<(Tenant Tenant, StaffMembership Membership)> AddTenantWithOwnerAsync(
        Fixture fx, string name, string slug, TenantStatus status = TenantStatus.Active)
    {
        var tenant = Tenant.Create(name, slug);

        if (status != TenantStatus.Pending)
        {
            tenant.ChangeStatus(TenantStatus.Active);

            if (status == TenantStatus.Suspended)
            {
                tenant.ChangeStatus(TenantStatus.Suspended);
            }
        }

        fx.Db.Tenants.Add(tenant);

        var membership = StaffMembership.Create(tenant.Id, UserId, OwnerRoleId);
        fx.Db.StaffMemberships.Add(membership);
        await fx.Db.SaveChangesAsync();

        fx.Memberships.Memberships.Add(new TenantMembershipInfo(
            membership.Id,
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            OwnerRoleId,
            "Owner",
            (int)membership.Status));

        fx.Memberships.AddMembership(membership.Id, UserId);

        return (tenant, membership);
    }

    public static void Grant(Fixture fx, Guid tenantId, string roleName, Guid roleId)
    {
        var membershipId = Guid.NewGuid();

        fx.Memberships.Memberships.Add(new TenantMembershipInfo(
            membershipId,
            tenantId,
            "Tenant",
            "tenant",
            roleId,
            roleName,
            (int)MembershipStatus.Active));

        fx.Memberships.AddMembership(membershipId, UserId);
    }

    public static async Task<Restaurant> AddRestaurantAsync(
        Fixture fx, Guid tenantId, string name, RestaurantStatus status = RestaurantStatus.Draft)
    {
        var address = Domain.ValueObjects.Address.Create("1 Main St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(tenantId, name, address);
        restaurant.ChangeStatus(status);
        fx.Db.Restaurants.Add(restaurant);
        await fx.Db.SaveChangesAsync();
        return restaurant;
    }

    public static WebsiteTemplate AddTemplate(
        Fixture fx, string name, string key, bool active = true)
    {
        var template = WebsiteTemplate.Create(name, key, $"{name} description");
        if (!active) template.SetActive(false);
        fx.Db.WebsiteTemplates.Add(template);
        fx.Db.SaveChanges();
        return template;
    }

    public static Website AddWebsite(
        Fixture fx, Guid tenantId, Guid restaurantId, Guid templateId, string slug)
    {
        var website = Website.Create(tenantId, restaurantId, templateId, "Site", slug);
        fx.Db.Websites.Add(website);
        fx.Db.SaveChanges();
        return website;
    }

    public static WebsiteService CreateWebsiteService(Fixture fx) =>
        new(fx.Db, fx.TenantContext, fx.CurrentUser.Object, fx.Memberships);

    public static WebsiteTemplateService CreateTemplateService(Fixture fx) =>
        new(fx.Db);

    public static PublicSiteService CreatePublicSiteService(Fixture fx) =>
        new(fx.Db);
}
