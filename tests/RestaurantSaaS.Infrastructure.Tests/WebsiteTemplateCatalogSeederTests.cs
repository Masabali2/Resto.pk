using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Infrastructure.Seed;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class WebsiteTemplateCatalogSeederTests
{
    [Fact]
    public async Task SeedAsync_InsertsTheTenCatalogueTemplates()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var templates = await fx.Db.WebsiteTemplates.ToListAsync();
        templates.Should().HaveCount(10);
        templates.Select(t => t.TemplateKey).Should().BeEquivalentTo(
            new[]
            {
                "spice-garden",
                "ember-oak",
                "urban-slice",
                "coastal-table",
                "bloom-cafe",
                "olive-grove",
                "midnight-sushi",
                "heritage-kitchen",
                "green-fork",
                "street-feast"
            });
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_AndDoesNotDuplicate()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);
        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);
        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var templates = await fx.Db.WebsiteTemplates.ToListAsync();
        templates.Should().HaveCount(10);
    }

    [Fact]
    public async Task SeedAsync_DoesNotOverwriteExistingPlatformAdminChanges()
    {
        var fx = TestFactory.Create();
        var existing = TestFactory.AddTemplate(fx, "Renamed By Admin", "spice-garden");
        existing.Update("Renamed By Admin", "Custom description", false);

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var reloaded = await fx.Db.WebsiteTemplates
            .SingleAsync(t => t.TemplateKey == "spice-garden");

        reloaded.Id.Should().Be(existing.Id);
        reloaded.Name.Should().Be("Renamed By Admin");
        reloaded.Description.Should().Be("Custom description");
        reloaded.IsActive.Should().BeFalse();
        reloaded.DesignConfigurationJson.Should().BeNull();

        (await fx.Db.WebsiteTemplates.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_StoresDesignConfigurationForEveryPreset()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var templates = await fx.Db.WebsiteTemplates.ToListAsync();

        templates.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.DesignConfigurationJson));

        foreach (var template in templates)
        {
            var error = Application.Common.WebsiteTemplateDesignSchema.Validate(
                template.DesignConfigurationJson);

            error.Should().BeNull($"template '{template.TemplateKey}' must ship a valid design configuration");
        }
    }

    [Fact]
    public async Task SeedAsync_PresetsUseDistinctTemplateKeys()
    {
        var keys = WebsiteTemplateCatalogSeeder.Presets.Select(p => p.TemplateKey).ToList();

        keys.Should().OnlyHaveUniqueItems();
        keys.Should().AllSatisfy(key =>
            key.Should().Be(key.ToLowerInvariant()));
    }

    [Fact]
    public async Task SeedAsync_PresetsUseDistinctPalettes()
    {
        var palettes = WebsiteTemplateCatalogSeeder.Presets
            .Select(p => Application.Common.WebsiteTemplateDesignSchema.Validate(p.DesignConfigurationJson))
            .ToList();

        palettes.Should().OnlyContain(error => error == null);

        var primaryColours = WebsiteTemplateCatalogSeeder.Presets
            .Select(p => ExtractPrimary(p.DesignConfigurationJson))
            .ToList();

        primaryColours.Should().OnlyHaveUniqueItems();
    }

    private static string ExtractPrimary(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("design")
            .GetProperty("palette")
            .GetProperty("primary")
            .GetString()!;
    }
}
