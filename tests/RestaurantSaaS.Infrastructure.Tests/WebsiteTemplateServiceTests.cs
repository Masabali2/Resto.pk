using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class WebsiteTemplateServiceTests
{
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
    public async Task ListAsync_ExcludesInactiveTemplates_ByDefault()
    {
        var fx = TestFactory.Create();
        TestFactory.AddTemplate(fx, "Classic", "classic");
        TestFactory.AddTemplate(fx, "Retired", "retired", active: false);

        var service = TestFactory.CreateTemplateService(fx);

        var result = await service.ListAsync(includeInactive: false);

        result.Should().ContainSingle(t => t.TemplateKey == "classic");
    }

    [Fact]
    public async Task ListAsync_IncludeReturnsInactiveTemplates()
    {
        var fx = TestFactory.Create();
        TestFactory.AddTemplate(fx, "Classic", "classic");
        TestFactory.AddTemplate(fx, "Retired", "retired", active: false);

        var service = TestFactory.CreateTemplateService(fx);

        var result = await service.ListAsync(includeInactive: true);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ThrowsNotFound()
    {
        var fx = TestFactory.Create();
        var service = TestFactory.CreateTemplateService(fx);

        var act = () => service.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_DuplicateKey_ThrowsConflict()
    {
        var fx = TestFactory.Create();
        TestFactory.AddTemplate(fx, "Classic", "classic");
        var service = TestFactory.CreateTemplateService(fx);

        var request = new CreateWebsiteTemplateRequest("Other", "CLASSIC", null, null);

        var act = () => service.CreateAsync(request);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateAsync_ValidTemplate_PersistsAndNormalizesKey()
    {
        var fx = TestFactory.Create();
        var service = TestFactory.CreateTemplateService(fx);

        var dto = await service.CreateAsync(
            new CreateWebsiteTemplateRequest(
                "Modern", "MODERN-Dine", "desc", null, ValidDesignConfiguration));

        dto.TemplateKey.Should().Be("modern-dine");
        dto.IsActive.Should().BeTrue();
        dto.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);
        fx.Db.WebsiteTemplates.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WithoutDesignConfiguration_Throws()
    {
        var fx = TestFactory.Create();
        var service = TestFactory.CreateTemplateService(fx);

        // A catalogue template is rendered by the generic template renderer,
        // so it can never be created without a default design configuration.
        var act = () => service.CreateAsync(
            new CreateWebsiteTemplateRequest("Modern", "modern-dine", "desc", null));

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_REQUIRED");
        fx.Db.WebsiteTemplates.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_RetiresTemplate_AndKeepsExistingWebsites()
    {
        var fx = TestFactory.Create();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var (tenant, _) = await TestFactory.AddTenantWithOwnerAsync(fx, "T1", "t1-tenant");
        var restaurant = await TestFactory.AddRestaurantAsync(fx, tenant.Id, "R1");
        var website = TestFactory.AddWebsite(fx, tenant.Id, restaurant.Id, template.Id, "site-1");

        var service = TestFactory.CreateTemplateService(fx);

        await service.UpdateAsync(template.Id, new UpdateWebsiteTemplateRequest(null, null, false));

        website.WebsiteTemplateId.Should().Be(template.Id);
        var reloaded = await fx.Db.WebsiteTemplates.FirstAsync(t => t.Id == template.Id);
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithDesignConfiguration_PersistsAndReturnsIt()
    {
        var fx = TestFactory.Create();
        var service = TestFactory.CreateTemplateService(fx);

        var dto = await service.CreateAsync(new CreateWebsiteTemplateRequest(
            "Spice Garden",
            "spice-garden",
            "Desi",
            null,
            ValidDesignConfiguration));

        dto.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);

        var stored = await fx.Db.WebsiteTemplates.SingleAsync(t => t.Id == dto.Id);
        stored.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);
    }

    [Fact]
    public async Task UpdateAsync_WithDesignConfiguration_ReplacesStoredValue()
    {
        var fx = TestFactory.Create();
        var template = TestFactory.AddTemplate(fx, "Classic", "classic");
        var service = TestFactory.CreateTemplateService(fx);

        var dto = await service.UpdateAsync(
            template.Id,
            new UpdateWebsiteTemplateRequest(null, null, null, ValidDesignConfiguration));

        dto.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);

        var stored = await fx.Db.WebsiteTemplates.SingleAsync(t => t.Id == template.Id);
        stored.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);
    }

    [Fact]
    public async Task UpdateAsync_WithoutDesignConfiguration_LeavesStoredValueUntouched()
    {
        var fx = TestFactory.Create();
        var template = TestFactory.AddTemplate(
            fx, "Classic", "classic", designConfigurationJson: ValidDesignConfiguration);
        var service = TestFactory.CreateTemplateService(fx);

        var dto = await service.UpdateAsync(
            template.Id,
            new UpdateWebsiteTemplateRequest("Renamed", null, null));

        dto.DesignConfigurationJson.Should().Be(ValidDesignConfiguration);
    }

    [Fact]
    public async Task CreateAsync_WithMalformedDesignConfiguration_ThrowsDomainException()
    {
        var fx = TestFactory.Create();
        var service = TestFactory.CreateTemplateService(fx);

        var act = () => service.CreateAsync(new CreateWebsiteTemplateRequest(
            "Broken",
            "broken",
            null,
            null,
            "{ not json"));

        await act.Should().ThrowAsync<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_INVALID");
    }
}
