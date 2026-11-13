using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
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

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());
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

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

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

    // ------------------------------------------------------------------
    // Publish / Unpublish lifecycle
    // ------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ValidDraftMenuWithActiveCategory_ChangesStatusToPublished()
    {
        var scenario = await CreatePublishScenarioAsync();

        var result = await scenario.Service.PublishAsync(scenario.Menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.IsActive.Should().BeTrue();

        var persisted = await scenario.Fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == scenario.Menu.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublishedMenu_IsIdempotent()
    {
        var scenario = await CreatePublishScenarioAsync();

        await scenario.Service.PublishAsync(scenario.Menu.Id);
        var result = await scenario.Service.PublishAsync(scenario.Menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
    }

    [Fact]
    public async Task PublishAsync_ArchivedMenu_ThrowsDomainException()
    {
        var scenario = await CreatePublishScenarioAsync();
        scenario.Menu.Archive();
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.PublishAsync(scenario.Menu.Id);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task PublishAsync_InactiveMenu_ThrowsDomainException()
    {
        var scenario = await CreatePublishScenarioAsync();
        scenario.Menu.SetActive(false);
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.PublishAsync(scenario.Menu.Id);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task PublishAsync_NoActiveCategories_ThrowsDomainException()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "Empty Tenant", "empty-tenant");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Empty Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Empty Menu");
        fx.Db.Menus.Add(menu);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());
        var act = () => service.PublishAsync(menu.Id);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task PublishAsync_MissingMenu_ThrowsNotFoundException()
    {
        var scenario = await CreatePublishScenarioAsync();
        var act = () => scenario.Service.PublishAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_CrossTenantMenu_ThrowsNotFoundException()
    {
        var scenario = await CreatePublishScenarioAsync();
        var otherTenant = Tenant.Create("Other", "other");
        otherTenant.ChangeStatus(TenantStatus.Active);
        scenario.Fx.Db.Tenants.Add(otherTenant);
        await scenario.Fx.Db.SaveChangesAsync();
        var otherRestaurant = await TestFactory.AddRestaurantAsync(
            scenario.Fx, otherTenant.Id, "Other Restaurant", RestaurantStatus.Active);
        var foreignMenu = Menu.Create(otherTenant.Id, otherRestaurant.Id, "Foreign Menu");
        scenario.Fx.Db.Menus.Add(foreignMenu);
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.PublishAsync(foreignMenu.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_WithoutMenuUpdatePermission_ThrowsForbiddenException()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var tenant = Tenant.Create("Kitchen Tenant", "kitchen-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        fx.Db.Tenants.Add(tenant);
        await fx.Db.SaveChangesAsync();
        fx.TenantContext.SetTenant(tenant.Id);
        TestFactory.Grant(fx, tenant.Id, "Kitchen", TestFactory.KitchenRoleId);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Kitchen Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Kitchen Menu");
        fx.Db.Menus.Add(menu);
        await fx.Db.SaveChangesAsync();
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());
        var act = () => service.PublishAsync(menu.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UnpublishAsync_PublishedMenu_ReturnsStatusToDraft()
    {
        var scenario = await CreatePublishScenarioAsync();
        await scenario.Service.PublishAsync(scenario.Menu.Id);

        var result = await scenario.Service.UnpublishAsync(scenario.Menu.Id);

        result.Status.Should().Be(MenuStatus.Draft.ToString());
        result.IsActive.Should().BeTrue();

        var persisted = await scenario.Fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == scenario.Menu.Id);
        persisted.Status.Should().Be(MenuStatus.Draft);
    }

    [Fact]
    public async Task UnpublishAsync_DraftMenu_ThrowsDomainException()
    {
        var scenario = await CreatePublishScenarioAsync();
        var act = () => scenario.Service.UnpublishAsync(scenario.Menu.Id);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UnpublishAsync_ArchivedMenu_ThrowsDomainException()
    {
        var scenario = await CreatePublishScenarioAsync();
        scenario.Menu.Archive();
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.UnpublishAsync(scenario.Menu.Id);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UnpublishAsync_CrossTenantMenu_ThrowsNotFoundException()
    {
        var scenario = await CreatePublishScenarioAsync();
        await scenario.Service.PublishAsync(scenario.Menu.Id);
        var otherTenant = Tenant.Create("Other", "other");
        otherTenant.ChangeStatus(TenantStatus.Active);
        scenario.Fx.Db.Tenants.Add(otherTenant);
        await scenario.Fx.Db.SaveChangesAsync();
        var otherRestaurant = await TestFactory.AddRestaurantAsync(
            scenario.Fx, otherTenant.Id, "Other Restaurant", RestaurantStatus.Active);
        var foreignMenu = Menu.Create(otherTenant.Id, otherRestaurant.Id, "Foreign Menu");
        scenario.Fx.Db.Menus.Add(foreignMenu);
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.UnpublishAsync(foreignMenu.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UnpublishAsync_WithoutMenuUpdatePermission_ThrowsForbiddenException()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var tenant = Tenant.Create("Kitchen Tenant", "kitchen-tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        fx.Db.Tenants.Add(tenant);
        await fx.Db.SaveChangesAsync();
        fx.TenantContext.SetTenant(tenant.Id);
        TestFactory.Grant(fx, tenant.Id, "Kitchen", TestFactory.KitchenRoleId);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Kitchen Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Kitchen Menu");
        fx.Db.Menus.Add(menu);
        await fx.Db.SaveChangesAsync();
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        // Permission (MenuUpdate) is validated before the menu/status is inspected,
        // so a Kitchen member is rejected even though a published menu exists.
        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());
        var act = () => service.UnpublishAsync(menu.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task PublishAsync_PersistsPublicUrlOnTheMenu()
    {
        var scenario = await CreatePublishScenarioAsync();

        var result = await scenario.Service.PublishAsync(scenario.Menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.PublicUrl.Should().Be("https://localhost:4200/r/pub-site/menu");

        var persisted = await scenario.Fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == scenario.Menu.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
        persisted.PublicUrl.Should().Be("https://localhost:4200/r/pub-site/menu");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPersistedPublicUrlAfterPublish()
    {
        var scenario = await CreatePublishScenarioAsync();

        await scenario.Service.PublishAsync(scenario.Menu.Id);

        var reloaded = await scenario.Service.GetByIdAsync(scenario.Menu.Id);

        reloaded.PublicUrl.Should().Be("https://localhost:4200/r/pub-site/menu");
        reloaded.Status.Should().Be(MenuStatus.Published.ToString());
    }

    [Fact]
    public async Task PublishAsync_PublicUrlIsTenantIsolated()
    {
        var scenario = await CreatePublishScenarioAsync();
        await scenario.Service.PublishAsync(scenario.Menu.Id);

        var otherTenant = Tenant.Create("Other", "other");
        otherTenant.ChangeStatus(TenantStatus.Active);
        scenario.Fx.Db.Tenants.Add(otherTenant);
        await scenario.Fx.Db.SaveChangesAsync();
        var otherRestaurant = await TestFactory.AddRestaurantAsync(
            scenario.Fx, otherTenant.Id, "Other Restaurant", RestaurantStatus.Active);
        var foreignMenu = Menu.Create(otherTenant.Id, otherRestaurant.Id, "Foreign Menu");
        scenario.Fx.Db.Menus.Add(foreignMenu);
        await scenario.Fx.Db.SaveChangesAsync();

        var act = () => scenario.Service.GetByIdAsync(foreignMenu.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UnpublishAsync_PreservesPersistedPublicUrl()
    {
        var scenario = await CreatePublishScenarioAsync();
        await scenario.Service.PublishAsync(scenario.Menu.Id);

        var result = await scenario.Service.UnpublishAsync(scenario.Menu.Id);

        result.Status.Should().Be(MenuStatus.Draft.ToString());

        var persisted = await scenario.Fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == scenario.Menu.Id);
        persisted.Status.Should().Be(MenuStatus.Draft);
        persisted.PublicUrl.Should().Be("https://localhost:4200/r/pub-site/menu");
    }

    [Fact]
    public async Task PublishAsync_RepublishKeepsPublicUrlStable()
    {
        var scenario = await CreatePublishScenarioAsync();

        var first = await scenario.Service.PublishAsync(scenario.Menu.Id);
        await scenario.Service.UnpublishAsync(scenario.Menu.Id);
        var second = await scenario.Service.PublishAsync(scenario.Menu.Id);

        first.PublicUrl.Should().Be("https://localhost:4200/r/pub-site/menu");
        second.PublicUrl.Should().Be(first.PublicUrl);
    }

    [Fact]
    public async Task PublishAsync_MissingPublishedWebsite_PublishesWithNullPublicUrl()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "No Website Tenant", "no-website-tenant");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "No Website Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Draft Menu");
        fx.Db.Menus.Add(menu);
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

        var result = await service.PublishAsync(menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.PublicUrl.Should().BeNull();

        var persisted = await fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == menu.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
        persisted.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_UnpublishedWebsite_PublishesWithNullPublicUrl()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "Unpublished Website Tenant", "unpublished-website-tenant");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Unpublished Website Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Draft Menu");
        fx.Db.Menus.Add(menu);
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        var template = WebsiteTemplate.Create("Unpublished Template", "unpublished-template");
        fx.Db.WebsiteTemplates.Add(template);
        var website = Website.Create(tenant.Id, restaurant.Id, template.Id, "Unpublished Site", "unpublished-site");
        fx.Db.Websites.Add(website);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

        var result = await service.PublishAsync(menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.PublicUrl.Should().BeNull();

        var persisted = await fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == menu.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
        persisted.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_WrongRestaurantWebsite_PublishesWithNullPublicUrl()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "Tenant A", "tenant-a");
        fx.TenantContext.SetTenant(tenant.Id);

        var restaurantA = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Restaurant A", RestaurantStatus.Active);
        var menuA = Menu.Create(tenant.Id, restaurantA.Id, "Menu A");
        fx.Db.Menus.Add(menuA);
        var categoryA = MenuCategory.Create(menuA.Id, "Burgers");
        fx.Db.MenuCategories.Add(categoryA);
        await fx.Db.SaveChangesAsync();

        var restaurantB = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Restaurant B", RestaurantStatus.Active);
        var template = WebsiteTemplate.Create("Published Template B", "published-template-b");
        fx.Db.WebsiteTemplates.Add(template);
        var websiteB = Website.Create(tenant.Id, restaurantB.Id, template.Id, "Published Site B", "published-site-b");
        var draftB = WebsiteConfiguration.CreateDraft(websiteB.Id, "{}", 1);
        websiteB.Publish(draftB, template, DateTime.UtcNow);
        fx.Db.Websites.Add(websiteB);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

        var result = await service.PublishAsync(menuA.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.PublicUrl.Should().BeNull();

        var persisted = await fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == menuA.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
        persisted.PublicUrl.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_RepairsPublishedMenuWithNullPublicUrl()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "Repair Tenant", "repair-tenant");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Repair Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Draft Menu");
        fx.Db.Menus.Add(menu);
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        menu.Publish(1);
        await fx.Db.SaveChangesAsync();

        var persistedBefore = await fx.Db.Menus.AsNoTracking().SingleAsync(m => m.Id == menu.Id);
        persistedBefore.Status.Should().Be(MenuStatus.Published);
        persistedBefore.PublicUrl.Should().BeNull();

        var template = WebsiteTemplate.Create("Repair Template", "repair-template");
        fx.Db.WebsiteTemplates.Add(template);
        var website = Website.Create(tenant.Id, restaurant.Id, template.Id, "Repair Site", "repair-site");
        var draft = WebsiteConfiguration.CreateDraft(website.Id, "{}", 1);
        website.Publish(draft, template, DateTime.UtcNow);
        fx.Db.Websites.Add(website);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

        var result = await service.PublishAsync(menu.Id);

        result.Status.Should().Be(MenuStatus.Published.ToString());
        result.PublicUrl.Should().Be("https://localhost:4200/r/repair-site/menu");

        var persisted = await fx.Db.Menus
            .AsNoTracking()
            .SingleAsync(m => m.Id == menu.Id);
        persisted.Status.Should().Be(MenuStatus.Published);
        persisted.PublicUrl.Should().Be("https://localhost:4200/r/repair-site/menu");
    }

    private static async Task<PublishScenario> CreatePublishScenarioAsync()
    {
        var fx = TestFactory.Create();
        fx.Db.DisableTenantFilters = false;
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "Pub Tenant", "pub-tenant");
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "Pub Restaurant", RestaurantStatus.Active);
        var menu = Menu.Create(tenant.Id, restaurant.Id, "Draft Menu");
        fx.Db.Menus.Add(menu);
        await fx.Db.SaveChangesAsync();
        var category = MenuCategory.Create(menu.Id, "Burgers");
        fx.Db.MenuCategories.Add(category);
        await fx.Db.SaveChangesAsync();

        var template = WebsiteTemplate.Create("Pub Template", "pub-template");
        fx.Db.WebsiteTemplates.Add(template);
        var website = Website.Create(tenant.Id, restaurant.Id, template.Id, "Pub Site", "pub-site");
        var draft = WebsiteConfiguration.CreateDraft(website.Id, "{}", 1);
        website.Publish(draft, template, DateTime.UtcNow);
        fx.Db.Websites.Add(website);
        await fx.Db.SaveChangesAsync();

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());
        return new PublishScenario(fx, tenant, restaurant, menu, service);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://localhost:4200"
            })
            .Build();

    private sealed record PublishScenario(
        Fixture Fx,
        Tenant Tenant,
        Restaurant Restaurant,
        Menu Menu,
        MenuService Service);

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

        var service = new MenuService(fx.Db, fx.AccessGuard, CreateConfiguration());

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
