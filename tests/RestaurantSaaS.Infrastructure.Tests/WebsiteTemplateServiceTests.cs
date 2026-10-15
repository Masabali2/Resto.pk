using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class WebsiteTemplateServiceTests
{
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
            new CreateWebsiteTemplateRequest("Modern", "MODERN-Dine", "desc", null));

        dto.TemplateKey.Should().Be("modern-dine");
        dto.IsActive.Should().BeTrue();
        fx.Db.WebsiteTemplates.Should().ContainSingle();
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
}
