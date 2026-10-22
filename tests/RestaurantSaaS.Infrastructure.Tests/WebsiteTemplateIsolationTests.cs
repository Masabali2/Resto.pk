using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// Isolation guarantees of the shared website template catalogue.
///
/// The catalogue rows belong to the PLATFORM and are shared by every tenant.
/// Reading a template (discovery/preview) and selecting a template for a
/// website must therefore never change a template row, and a tenant must
/// never reach the websites of another tenant.
/// </summary>
public sealed class WebsiteTemplateIsolationTests
{
    private const string ValidDraftJson = """{"schemaVersion":1,"sections":[{"type":"hero","title":"Welcome"}]}""";

    private const string ValidDesignConfiguration = """
    {"schemaVersion":1,"category":"Fine dining","tags":["premium"],"featured":true,"displayOrder":2,
    "design":{"palette":{"background":"#F7F2EA","surface":"#FFFFFF","text":"#1C1B1A","mutedText":"#6B6560",
    "primary":"#2E2A27","accent":"#B0672A","border":"#E3D9CB"},
    "typography":{"headingFont":"serif-editorial","bodyFont":"sans-modern"},
    "layout":{"header":"centered","hero":"cinematic","menu":"editorial-list","gallery":"strip",
    "footer":"simple","buttons":"outline","corners":"sharp"},
    "sections":[{"type":"hero","enabled":true,"order":1}]},
    "previewContent":{"eyebrow":"An evening","heroTitle":"Ember, Oak & Patience",
    "heroDescription":"Seasonal plates over live fire.","primaryActionLabel":"Reserve a Table",
    "sampleDishes":[{"name":"Dry-Aged Ribeye","price":"PKR 5,400"}]}}
    """;

    [Fact]
    public async Task ListAvailableTemplatesAsync_DoesNotModifyTheSharedTemplateRows()
    {
        var (fx, service) = await CreateAuthorizedFixtureAsync();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);

        var before = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        await service.ListAvailableTemplatesAsync();
        await service.ListAvailableTemplatesAsync();

        var after = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        after.Name.Should().Be(before.Name);
        after.TemplateKey.Should().Be(before.TemplateKey);
        after.IsActive.Should().Be(before.IsActive);
        after.DesignConfigurationJson.Should().Be(before.DesignConfigurationJson);
        after.PreviewImageFileId.Should().Be(before.PreviewImageFileId);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_DoesNotModifyTheSharedTemplateRows()
    {
        var (fx, service, _, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);

        var before = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        await service.CreateAsync(
            new CreateWebsiteRequest(restaurant.Id, template.Id, "Site One", "site-one"));

        var after = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        after.Name.Should().Be(before.Name);
        after.TemplateKey.Should().Be(before.TemplateKey);
        after.IsActive.Should().Be(before.IsActive);
        after.DesignConfigurationJson.Should().Be(before.DesignConfigurationJson);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
    }

    [Fact]
    public async Task SaveDraftAsync_DoesNotModifyTheSharedTemplateRows()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        var before = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));

        var after = await fx.Db.WebsiteTemplates.AsNoTracking().SingleAsync(t => t.Id == template.Id);

        after.DesignConfigurationJson.Should().Be(before.DesignConfigurationJson);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
    }

    [Fact]
    public async Task SaveDraftAsync_DoesNotModifyAnotherTenantsWebsite()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");

        var ownWebsite = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "own-site");
        var foreignWebsite = TestFactory.AddWebsite(
            fx, Guid.NewGuid(), restaurant.Id, template.Id, "foreign-site");

        var foreignBefore = await ReadWithoutTenantFiltersAsync(
            fx, w => w.Id == foreignWebsite.Id);

        await service.SaveDraftAsync(ownWebsite.Id, new SaveWebsiteDraftRequest(ValidDraftJson));

        var foreignAfter = await ReadWithoutTenantFiltersAsync(
            fx, w => w.Id == foreignWebsite.Id);

        foreignAfter.Name.Should().Be(foreignBefore.Name);
        foreignAfter.WebsiteTemplateId.Should().Be(foreignBefore.WebsiteTemplateId);
        foreignAfter.IsPublished.Should().Be(foreignBefore.IsPublished);
        foreignAfter.UpdatedAt.Should().Be(foreignBefore.UpdatedAt);

        // The foreign website must not have gained a draft of its own.
        fx.Db.DisableTenantFilters = true;
        try
        {
            var foreignConfigurations = await fx.Db.WebsiteConfigurations
                .Where(c => c.WebsiteId == foreignWebsite.Id)
                .CountAsync();

            foreignConfigurations.Should().Be(0);
        }
        finally
        {
            fx.Db.DisableTenantFilters = false;
        }

        // The tenant query filter keeps the foreign website out of reach of
        // the current tenant in the first place.
        var visible = await fx.Db.Websites
            .AnyAsync(w => w.Id == foreignWebsite.Id);

        visible.Should().BeFalse();
    }

    private static async Task<Domain.Entities.Websites.Website> ReadWithoutTenantFiltersAsync(
        Fixture fx,
        System.Linq.Expressions.Expression<Func<Domain.Entities.Websites.Website, bool>> predicate)
    {
        fx.Db.DisableTenantFilters = true;
        try
        {
            return await fx.Db.Websites.AsNoTracking().SingleAsync(predicate);
        }
        finally
        {
            fx.Db.DisableTenantFilters = false;
        }
    }

    [Fact]
    public async Task SaveDraftAsync_ForAnotherTenantsWebsite_ThrowsNotFound()
    {
        var (fx, service, _, _) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var foreignWebsite = TestFactory.AddWebsite(
            fx, Guid.NewGuid(), Guid.NewGuid(), template.Id, "foreign-site");

        var act = () => service.SaveDraftAsync(
            foreignWebsite.Id, new SaveWebsiteDraftRequest(ValidDraftJson));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_ForAnotherTenantsWebsite_ThrowsNotFound()
    {
        var (fx, service, _, _) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var foreignWebsite = TestFactory.AddWebsite(
            fx, Guid.NewGuid(), Guid.NewGuid(), template.Id, "foreign-site");

        var act = () => service.PublishAsync(foreignWebsite.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_RetiringATemplate_KeepsExistingWebsitesAndTheirDrafts()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));

        var templateService = TestFactory.CreateTemplateService(fx);
        await templateService.UpdateAsync(
            template.Id, new UpdateWebsiteTemplateRequest(null, null, false));

        var reloaded = await fx.Db.WebsiteTemplates.SingleAsync(t => t.Id == template.Id);
        reloaded.IsActive.Should().BeFalse();
        reloaded.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);

        // The website of the tenant keeps its template reference and its draft.
        var reloadedWebsite = await fx.Db.Websites.SingleAsync(w => w.Id == website.Id);
        reloadedWebsite.WebsiteTemplateId.Should().Be(template.Id);

        var draft = await fx.Db.WebsiteConfigurations.SingleAsync(c => c.WebsiteId == website.Id);
        draft.ConfigurationJson.Should().Be(ValidDraftJson);
    }

    [Fact]
    public async Task TemplateUpdate_DoesNotTouchTheWebsitesThatUseTheTemplate()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        var templateService = TestFactory.CreateTemplateService(fx);
        await templateService.UpdateAsync(
            template.Id,
            new UpdateWebsiteTemplateRequest(null, null, null, """{"schemaVersion":1}"""));

        var reloadedWebsite = await fx.Db.Websites.SingleAsync(w => w.Id == website.Id);
        reloadedWebsite.WebsiteTemplateId.Should().Be(template.Id);
        reloadedWebsite.UpdatedAt.Should().Be(website.UpdatedAt);
    }

    [Fact]
    public async Task DomainRejectsATemplateDesignConfigurationThatIsNotJson()
    {
        var fx = TestFactory.Create();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");

        var act = () => template.SetDesignConfiguration("{ not json");

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_INVALID");
    }

    private static Task<(Fixture Fx, WebsiteService Service)> CreateAuthorizedFixtureAsync()
    {
        var fx = TestFactory.Create();
        TestFactory.SeedRolesAsync(fx.Db).GetAwaiter().GetResult();
        var (tenant, _) = TestFactory.AddTenantWithOwnerAsync(fx, "Tenant One", "tenant-one")
            .GetAwaiter().GetResult();
        fx.TenantContext.SetTenant(tenant.Id);
        return Task.FromResult((fx, TestFactory.CreateWebsiteService(fx)));
    }

    private static Task<(Fixture Fx, WebsiteService Service, Domain.Entities.Tenants.Tenant Tenant, Domain.Entities.Restaurants.Restaurant Restaurant)>
        CreateWithRestaurantAsync()
    {
        var fx = TestFactory.Create();
        TestFactory.SeedRolesAsync(fx.Db).GetAwaiter().GetResult();
        var (tenant, _) = TestFactory.AddTenantWithOwnerAsync(fx, "Tenant One", "tenant-one")
            .GetAwaiter().GetResult();
        fx.TenantContext.SetTenant(tenant.Id);
        var restaurant = TestFactory.AddRestaurantAsync(fx, tenant.Id, "R1").GetAwaiter().GetResult();
        return Task.FromResult((fx, TestFactory.CreateWebsiteService(fx), tenant, restaurant));
    }
}
