using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class WebsiteServiceTemplateSelectionTests
{
    private const string ValidDraftJson = """{"schemaVersion":1,"sections":[{"type":"hero","title":"Welcome"}]}""";

    [Fact]
    public async Task ListAvailableTemplatesAsync_ReturnsOnlyActiveTemplates()
    {
        var (fx, service) = await CreateAuthorizedFixtureAsync();
        TestFactory.AddTemplate(fx, "Classic", "classic");
        TestFactory.AddTemplate(fx, "Retired", "retired", active: false);

        var result = await service.ListAvailableTemplatesAsync();

        result.Should().ContainSingle(t => t.TemplateKey == "classic");
    }

    [Fact]
    public async Task ListAvailableTemplatesAsync_WithoutMembership_ThrowsCrossTenant()
    {
        var fx = TestFactory.Create(Guid.NewGuid());
        var service = TestFactory.CreateWebsiteService(fx);

        var act = () => service.ListAvailableTemplatesAsync();

        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task CreateAsync_SelectsActiveTemplate_ForOwnedRestaurant()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");

        var dto = await service.CreateAsync(
            new CreateWebsiteRequest(restaurant.Id, template.Id, "Site One", "site-one"));

        dto.WebsiteTemplateId.Should().Be(template.Id);
        dto.TemplateName.Should().Be("Classic");
        dto.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_InactiveTemplate_IsRejected()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Retired", "retired", active: false);

        var act = () => service.CreateAsync(
            new CreateWebsiteRequest(restaurant.Id, template.Id, "Site One", "site-one"));

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "WEBSITE_RULE");
    }

    [Fact]
    public async Task CreateAsync_UnknownTemplate_ThrowsNotFound()
    {
        var (fx, service, _, restaurant) = await CreateWithRestaurantAsync();

        var act = () => service.CreateAsync(
            new CreateWebsiteRequest(restaurant.Id, Guid.NewGuid(), "Site One", "site-one"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_RestaurantOfAnotherTenant_ThrowsNotFound()
    {
        var (fx, service, _, _) = await CreateWithRestaurantAsync();
        var otherTenantId = Guid.NewGuid();
        var otherRestaurant = await TestFactory.AddRestaurantAsync(fx, otherTenantId, "Other");
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");

        var act = () => service.CreateAsync(
            new CreateWebsiteRequest(otherRestaurant.Id, template.Id, "Hijack", "hijack"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ChangesTemplateWithinTenant_AndPreservesDraftConfig()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var classic = TestFactory.AddTemplate(fx, "Classic", "classic");
        var modern = TestFactory.AddTemplate(fx, "Modern", "modern");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, classic.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));

        var updated = await service.UpdateAsync(
            website.Id, new UpdateWebsiteRequest(null, null, modern.Id));

        updated.WebsiteTemplateId.Should().Be(modern.Id);
        updated.TemplateName.Should().Be("Modern");
        updated.DraftVersion.Should().Be(1);

        var draft = await fx.Db.WebsiteConfigurations.SingleAsync(c => c.WebsiteId == website.Id);
        draft.ConfigurationJson.Should().Be(ValidDraftJson);
        updated.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_InactiveTemplateSelection_IsRejected()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var classic = TestFactory.AddTemplate(fx, "Classic", "classic");
        var retired = TestFactory.AddTemplate(fx, "Retired", "retired", active: false);
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, classic.Id, "site-1");

        var act = () => service.UpdateAsync(
            website.Id, new UpdateWebsiteRequest(null, null, retired.Id));

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "WEBSITE_RULE");
    }

    [Fact]
    public async Task GetByIdAsync_OtherTenantsWebsite_ThrowsNotFound()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var foreignWebsite = TestFactory.AddWebsite(
            fx, Guid.NewGuid(), restaurant.Id, template.Id, "foreign-site");

        var act = () => service.GetByIdAsync(foreignWebsite.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_DoesNotPublish_WhenTemplateIsInactive()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));
        template.SetActive(false);
        await fx.Db.SaveChangesAsync();

        var act = () => service.PublishAsync(website.Id);

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "WEBSITE_TEMPLATE_INACTIVE");
        website.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeTemplate_AfterPublish_KeepsPublishedSiteOnline()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var classic = TestFactory.AddTemplate(fx, "Classic", "classic");
        var modern = TestFactory.AddTemplate(fx, "Modern", "modern");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, classic.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));
        await service.PublishAsync(website.Id);

        var updated = await service.UpdateAsync(
            website.Id, new UpdateWebsiteRequest(null, null, modern.Id));

        updated.IsPublished.Should().BeTrue();
        updated.Status.Should().Be(nameof(WebsiteStatus.Published));
        updated.WebsiteTemplateId.Should().Be(modern.Id);
        updated.PublishedVersion.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_PromotesDraft_AndPublicSiteResolvesPublishedContent()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));
        var published = await service.PublishAsync(website.Id);

        published.IsPublished.Should().BeTrue();
        published.Status.Should().Be(nameof(WebsiteStatus.Published));
        published.PublishedVersion.Should().Be(1);
        published.PublishedAt.Should().NotBeNull();

        var publicService = TestFactory.CreatePublicSiteService(fx);
        var site = await publicService.GetBySlugAsync("site-1");

        site.ConfigurationJson.Should().Be(ValidDraftJson);
        site.Template.TemplateKey.Should().Be("classic");
        site.Restaurant.Name.Should().Be(restaurant.Name);
    }

    [Fact]
    public async Task UnpublishAsync_RemovesPublicResolution_ButKeepsConfiguration()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        await service.SaveDraftAsync(website.Id, new SaveWebsiteDraftRequest(ValidDraftJson));
        await service.PublishAsync(website.Id);

        var result = await service.UnpublishAsync(website.Id);

        result.IsPublished.Should().BeFalse();
        result.Status.Should().Be(nameof(WebsiteStatus.Unpublished));

        var publicService = TestFactory.CreatePublicSiteService(fx);
        var act = () => publicService.GetBySlugAsync("site-1");
        await act.Should().ThrowAsync<NotFoundException>();

        var draft = await fx.Db.WebsiteConfigurations.SingleAsync(c => c.WebsiteId == website.Id);
        draft.ConfigurationJson.Should().Be(ValidDraftJson);
    }

    [Fact]
    public async Task PublicSite_UnpublishedSlug_ThrowsNotFound()
    {
        var (fx, _, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "draft-site");

        var publicService = TestFactory.CreatePublicSiteService(fx);

        var unknown = () => publicService.GetBySlugAsync("no-such-site");
        var draft = () => publicService.GetBySlugAsync("draft-site");

        await unknown.Should().ThrowAsync<NotFoundException>();
        await draft.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_WithoutDraft_IsRejected()
    {
        var (fx, service, tenant, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        var act = () => service.PublishAsync(website.Id);

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "WEBSITE_RULE");
        website.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_ForeignWebsite_ThrowsNotFound()
    {
        var (fx, service, _, restaurant) = await CreateWithRestaurantAsync();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var foreignWebsite = TestFactory.AddWebsite(
            fx, Guid.NewGuid(), restaurant.Id, template.Id, "foreign-site");

        var act = () => service.PublishAsync(foreignWebsite.Id);

        await act.Should().ThrowAsync<NotFoundException>();
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
