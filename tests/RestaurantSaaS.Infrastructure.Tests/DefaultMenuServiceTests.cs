using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Seed;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Behaviour of the approved default-menu catalogue and the deep-copy
/// operation that provisions it into a tenant-owned restaurant.
/// </summary>
public sealed class DefaultMenuServiceTests
{
    [Fact]
    public async Task GetDefaultAsync_ShouldReturnTheSeededDefaultMenu()
    {
        var scenario = await CreateScenarioAsync();

        var result = await scenario.Service.GetDefaultAsync();

        result.Id.Should().Be(DemoMenuSeeder.DemoMenuId);
        result.Name.Should().Be(DemoMenuSeeder.MenuName);
        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.CategoryCount.Should().Be(10);
        result.ItemCount.Should().Be(100);
        result.Categories.Should().HaveCount(10);
        result.Categories.Sum(category => category.Items.Count).Should().Be(100);
        result.Categories.Select(category => category.Name).Should().ContainInOrder(
            "Starters & Snacks",
            "Soups & Salads",
            "BBQ & Grill",
            "Karahi & Handi",
            "Curries & Daal",
            "Rice & Biryani",
            "Breads & Roti",
            "Fast Food",
            "Desserts",
            "Beverages");
    }

    [Fact]
    public async Task GetDefaultAsync_ShouldNotExposeUnrelatedTenantMenus()
    {
        var scenario = await CreateScenarioAsync();

        var unrelated = Menu.Create(
            scenario.Tenant.Id,
            scenario.Restaurant.Id,
            "Private tenant menu");
        scenario.Fx.Db.Menus.Add(unrelated);
        await scenario.Fx.Db.SaveChangesAsync();

        var result = await scenario.Service.GetDefaultAsync();

        result.Id.Should().Be(DemoMenuSeeder.DemoMenuId);
        result.Name.Should().Be(DemoMenuSeeder.MenuName);
        result.Categories.Should().NotContain(category => category.Name == "Private tenant menu");
        result.Categories.SelectMany(category => category.Items)
            .Should().NotContain(item => item.Name == "Private tenant menu");
    }

    [Fact]
    public async Task CopyDefaultAsync_ShouldDeepCopyEveryCategoryAndItemWithNewIds()
    {
        var scenario = await CreateScenarioAsync();
        var source = await LoadSourceAsync(scenario.Fx);

        var copied = await scenario.Service.CopyDefaultAsync(scenario.Restaurant.Id);

        copied.Id.Should().NotBe(DemoMenuSeeder.DemoMenuId);
        copied.TenantId.Should().Be(scenario.Tenant.Id);
        copied.RestaurantId.Should().Be(scenario.Restaurant.Id);
        copied.Name.Should().Be(DemoMenuSeeder.MenuName);
        copied.Status.Should().Be(MenuStatus.Draft.ToString());

        var copiedCategories = await scenario.Fx.Db.MenuCategories
            .AsNoTracking()
            .Where(category => category.MenuId == copied.Id)
            .ToListAsync();

        var copiedItems = await scenario.Fx.Db.MenuItems
            .AsNoTracking()
            .Where(item => item.Category.MenuId == copied.Id)
            .ToListAsync();

        copiedCategories.Should().HaveCount(10);
        copiedItems.Should().HaveCount(100);

        var sourceCategoryIds = source.Categories.Select(category => category.Id).ToHashSet();
        var sourceItemIds = source.Items.Select(item => item.Id).ToHashSet();

        copiedCategories.Should().OnlyHaveUniqueItems(category => category.Id);
        copiedItems.Should().OnlyHaveUniqueItems(item => item.Id);
        copiedCategories.Should().NotContain(category => sourceCategoryIds.Contains(category.Id));
        copiedItems.Should().NotContain(item => sourceItemIds.Contains(item.Id));
    }

    [Fact]
    public async Task CopyDefaultAsync_ShouldPreserveCatalogueDataAndDisplayOrder()
    {
        var scenario = await CreateScenarioAsync();
        var source = await LoadSourceAsync(scenario.Fx);

        var copied = await scenario.Service.CopyDefaultAsync(scenario.Restaurant.Id);

        var copiedCategories = await scenario.Fx.Db.MenuCategories
            .AsNoTracking()
            .Where(category => category.MenuId == copied.Id)
            .OrderBy(category => category.DisplayOrder)
            .ToListAsync();

        copiedCategories
            .Select(category => (category.Name, category.Description, category.DisplayOrder, category.IsActive))
            .Should()
            .BeEquivalentTo(
                source.Categories
                    .OrderBy(category => category.DisplayOrder)
                    .Select(category => (category.Name, category.Description, category.DisplayOrder, category.IsActive)),
                options => options.WithStrictOrdering());

        var copiedItems = await scenario.Fx.Db.MenuItems
            .AsNoTracking()
            .Where(item => item.Category.MenuId == copied.Id)
            .Select(item => new
            {
                item.Name,
                item.Description,
                item.Price,
                item.Currency,
                item.DisplayOrder,
                item.IsAvailable,
                item.IsActive,
                CategoryId = item.MenuCategoryId
            })
            .ToListAsync();

        var copiedCategoryNameById = copiedCategories.ToDictionary(category => category.Id, category => category.Name);
        var sourceCategoryNameById = source.Categories.ToDictionary(category => category.Id, category => category.Name);

        var sourceItemLookup = source.Items.ToDictionary(
            item => (sourceCategoryNameById[item.MenuCategoryId], item.Name));

        foreach (var copiedItem in copiedItems)
        {
            var key = (copiedCategoryNameById[copiedItem.CategoryId], copiedItem.Name);
            sourceItemLookup.Should().ContainKey(key);

            var sourceItem = sourceItemLookup[key];
            copiedItem.Description.Should().Be(sourceItem.Description);
            copiedItem.Price.Should().Be(sourceItem.Price);
            copiedItem.Currency.Should().Be(sourceItem.Currency);
            copiedItem.DisplayOrder.Should().Be(sourceItem.DisplayOrder);
            copiedItem.IsAvailable.Should().Be(sourceItem.IsAvailable);
            copiedItem.IsActive.Should().Be(sourceItem.IsActive);
        }
    }

    [Fact]
    public async Task CopyDefaultAsync_ShouldLeaveTheOriginalDefaultMenuUnchanged()
    {
        var scenario = await CreateScenarioAsync();
        var before = await LoadSourceAsync(scenario.Fx);

        await scenario.Service.CopyDefaultAsync(scenario.Restaurant.Id);

        var afterMenu = await scenario.Fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(menu => menu.Id == DemoMenuSeeder.DemoMenuId);

        var afterCategories = await scenario.Fx.Db.MenuCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(category => category.MenuId == DemoMenuSeeder.DemoMenuId)
            .ToListAsync();

        var afterItems = await scenario.Fx.Db.MenuItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Category.MenuId == DemoMenuSeeder.DemoMenuId)
            .ToListAsync();

        afterMenu.Name.Should().Be(before.Menu.Name);
        afterMenu.Description.Should().Be(before.Menu.Description);
        afterMenu.Status.Should().Be(before.Menu.Status);
        afterMenu.IsActive.Should().Be(before.Menu.IsActive);
        afterMenu.UpdatedAt.Should().Be(before.Menu.UpdatedAt);

        afterCategories.Should().HaveCount(before.Categories.Count);
        afterItems.Should().HaveCount(before.Items.Count);
        afterCategories.Select(category => category.Id).Should()
            .BeEquivalentTo(before.Categories.Select(category => category.Id));
        afterItems.Select(item => item.Id).Should()
            .BeEquivalentTo(before.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task CopyDefaultAsync_ShouldRejectAnotherTenantsRestaurant()
    {
        var scenario = await CreateScenarioAsync();

        var otherTenant = Tenant.Create("Other tenant", "other-tenant");
        otherTenant.ChangeStatus(TenantStatus.Active);
        scenario.Fx.Db.Tenants.Add(otherTenant);
        await scenario.Fx.Db.SaveChangesAsync();

        var otherRestaurant = await TestFactory.AddRestaurantAsync(
            scenario.Fx,
            otherTenant.Id,
            "Other restaurant",
            RestaurantStatus.Active);

        var act = () => scenario.Service.CopyDefaultAsync(otherRestaurant.Id);

        await act.Should().ThrowAsync<NotFoundException>();

        var menusForOtherRestaurant = await scenario.Fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(menu => menu.RestaurantId == otherRestaurant.Id);

        menusForOtherRestaurant.Should().Be(0);
    }

    [Fact]
    public async Task CopyDefaultAsync_ShouldRejectAUserWithoutMenuCreatePermission()
    {
        var fx = TestFactory.Create();
        await DemoMenuSeeder.SeedAsync(fx.Db);
        fx.Db.DisableTenantFilters = false;

        var tenant = Tenant.Create("Kitchen only", "kitchen-only");
        tenant.ChangeStatus(TenantStatus.Active);
        fx.Db.Tenants.Add(tenant);
        await fx.Db.SaveChangesAsync();

        fx.TenantContext.SetTenant(tenant.Id);
        TestFactory.Grant(fx, tenant.Id, "Kitchen", TestFactory.KitchenRoleId);

        var restaurant = await TestFactory.AddRestaurantAsync(
            fx,
            tenant.Id,
            "Kitchen restaurant",
            RestaurantStatus.Active);

        var service = new MenuService(fx.Db, fx.AccessGuard);
        var act = () => service.CopyDefaultAsync(restaurant.Id);

        await act.Should().ThrowAsync<ForbiddenException>();

        var menuCount = await fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(menu => menu.RestaurantId == restaurant.Id);

        menuCount.Should().Be(0);
    }

    [Fact]
    public async Task DefaultMenuOperations_ShouldReturnAControlledErrorWhenTheSourceIsMissing()
    {
        var fx = TestFactory.Create();
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "No demo", "no-demo");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(
            fx,
            tenant.Id,
            "No demo restaurant",
            RestaurantStatus.Active);

        var service = new MenuService(fx.Db, fx.AccessGuard);

        await FluentActions
            .Awaiting(() => service.GetDefaultAsync())
            .Should()
            .ThrowAsync<NotFoundException>();

        await FluentActions
            .Awaiting(() => service.CopyDefaultAsync(restaurant.Id))
            .Should()
            .ThrowAsync<NotFoundException>();

        var menuCount = await fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(menu => menu.RestaurantId == restaurant.Id);

        menuCount.Should().Be(0);
    }

    [Fact]
    public async Task DefaultMenuOperations_ShouldRejectAnArchivedSource()
    {
        var scenario = await CreateScenarioAsync();

        var sourceMenu = await scenario.Fx.Db.Menus
            .IgnoreQueryFilters()
            .SingleAsync(menu => menu.Id == DemoMenuSeeder.DemoMenuId);
        sourceMenu.Archive();
        await scenario.Fx.Db.SaveChangesAsync();

        await FluentActions
            .Awaiting(() => scenario.Service.GetDefaultAsync())
            .Should()
            .ThrowAsync<NotFoundException>();

        await FluentActions
            .Awaiting(() => scenario.Service.CopyDefaultAsync(scenario.Restaurant.Id))
            .Should()
            .ThrowAsync<NotFoundException>();

        var menuCount = await scenario.Fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(menu => menu.RestaurantId == scenario.Restaurant.Id);

        menuCount.Should().Be(0);
    }

    private static async Task<Scenario> CreateScenarioAsync()
    {
        var fx = TestFactory.Create();

        await DemoMenuSeeder.SeedAsync(fx.Db);
        fx.Db.DisableTenantFilters = false;

        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(
            fx,
            "Acme Kitchens",
            "acme-kitchens");

        fx.TenantContext.SetTenant(tenant.Id);

        var restaurant = await TestFactory.AddRestaurantAsync(
            fx,
            tenant.Id,
            "Acme Downtown",
            RestaurantStatus.Active);

        var service = new MenuService(fx.Db, fx.AccessGuard);

        return new Scenario(fx, tenant, restaurant, service);
    }

    private static async Task<SourceCatalogue> LoadSourceAsync(Fixture fx)
    {
        var menu = await fx.Db.Menus
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == DemoMenuSeeder.DemoMenuId);

        var categories = await fx.Db.MenuCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(category => category.MenuId == DemoMenuSeeder.DemoMenuId)
            .OrderBy(category => category.DisplayOrder)
            .ToListAsync();

        var items = await fx.Db.MenuItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Category.MenuId == DemoMenuSeeder.DemoMenuId)
            .ToListAsync();

        return new SourceCatalogue(menu, categories, items);
    }

    private sealed record Scenario(
        Fixture Fx,
        Tenant Tenant,
        Restaurant Restaurant,
        MenuService Service);

    private sealed record SourceCatalogue(
        Menu Menu,
        IReadOnlyList<MenuCategory> Categories,
        IReadOnlyList<MenuItem> Items);
}
