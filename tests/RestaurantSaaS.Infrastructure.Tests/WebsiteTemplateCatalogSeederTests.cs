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

    [Fact]
    public async Task SeedAsync_StoresTheSpiceGardenImageRegistryAndPlacements()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var template = await fx.Db.WebsiteTemplates
            .SingleAsync(t => t.TemplateKey == "spice-garden");

        using var document = System.Text.Json.JsonDocument.Parse(
            template.DesignConfigurationJson!);

        var root = document.RootElement;
        var assets = root.GetProperty("assets").EnumerateArray().ToList();

        assets.Should().HaveCount(8);

        var ids = assets
            .Select(asset => asset.GetProperty("assetId").GetString()!)
            .ToList();

        ids.Should().OnlyHaveUniqueItems();
        ids.Should().OnlyContain(id => id.StartsWith("sg-", StringComparison.Ordinal));

        assets.Should().OnlyContain(asset =>
            asset.GetProperty("url").GetString()!.StartsWith(
                "/assets/images/SpiceGarden/",
                StringComparison.Ordinal));

        assets.Should().OnlyContain(asset =>
            asset.GetProperty("url").GetString()!.IndexOf("://", StringComparison.Ordinal) < 0);

        assets.Should().OnlyContain(asset =>
            !string.IsNullOrWhiteSpace(asset.GetProperty("alt").GetString()));

        var previewContent = root.GetProperty("previewContent");

        ids.Should().Contain(previewContent.GetProperty("heroAssetId").GetString());
        ids.Should().Contain(previewContent.GetProperty("aboutAssetId").GetString());

        previewContent
            .GetProperty("galleryAssetIds")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .Should()
            .Equal(
                "sg-spicegarden8-png",
                "sg-spicegarden3-png",
                "sg-spicegarden4-png",
                "sg-spicegarden5-png",
                "sg-spicegarden6-png",
                "sg-spicegarden7-png");
    }

    [Fact]
    public async Task SeedAsync_StoresTheManifestRegistryForEveryTemplateWithImages()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var templates = await fx.Db.WebsiteTemplates.ToListAsync();

        foreach (var templateKey in new[] { "ember-oak", "urban-slice", "coastal-table" })
        {
            var template = templates.Single(t => t.TemplateKey == templateKey);
            var manifest = WebsiteTemplateAssetManifestRegistry.Find(templateKey)!;

            using var document = System.Text.Json.JsonDocument.Parse(
                template.DesignConfigurationJson!);

            var root = document.RootElement;
            var assets = root.GetProperty("assets").EnumerateArray().ToList();

            assets.Should().HaveCount(manifest.Assets.Count);
            assets.Select(asset => asset.GetProperty("assetId").GetString())
                .Should().Equal(manifest.Assets.Select(asset => asset.AssetId));

            var previewContent = root.GetProperty("previewContent");

            previewContent.GetProperty("heroAssetId").GetString()
                .Should().Be(manifest.Placements.HeroAssetId);
            previewContent.GetProperty("aboutAssetId").GetString()
                .Should().Be(manifest.Placements.AboutAssetId);
            previewContent.GetProperty("galleryAssetIds").EnumerateArray()
                .Select(entry => entry.GetString())
                .Should().Equal(manifest.Placements.GalleryAssetIds);
        }
    }

    [Fact]
    public async Task SeedAsync_LeavesTemplatesWithoutImagesUnchanged()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var others = await fx.Db.WebsiteTemplates
            .Where(t => !new[] { "spice-garden", "ember-oak", "urban-slice", "coastal-table" }
                .Contains(t.TemplateKey))
            .ToListAsync();

        others.Should().HaveCount(6);

        foreach (var template in others)
        {
            var preset = WebsiteTemplateCatalogSeeder.Presets
                .Single(p => p.TemplateKey == template.TemplateKey);

            template.DesignConfigurationJson.Should().Be(preset.DesignConfigurationJson);
            template.DesignConfigurationJson!.Should().NotContain("\"assets\"");
            template.DesignConfigurationJson!.Should().NotContain("\"heroAssetId\"");
        }
    }

    [Fact]
    public void ConfigurationBuilder_IsDeterministicForEveryManifestTemplate()
    {
        foreach (var preset in WebsiteTemplateCatalogSeeder.Presets)
        {
            var first = WebsiteTemplateAssetConfigurationBuilder.Apply(
                preset.TemplateKey,
                preset.DesignConfigurationJson);
            var second = WebsiteTemplateAssetConfigurationBuilder.Apply(
                preset.TemplateKey,
                preset.DesignConfigurationJson);

            first.Should().Be(second);
        }
    }

    [Fact]
    public async Task SeedAsync_ExposesManifestImagesThroughThePublicCatalogue()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var service = TestFactory.CreateTemplateService(fx);
        var templates = await service.ListPublicAsync();

        var spiceGarden = templates.Single(t => t.TemplateKey == "spice-garden");

        spiceGarden.DesignConfigurationJson.Should().NotBeNull();
        spiceGarden.DesignConfigurationJson!.Should().Contain("\"assets\"");
        spiceGarden.DesignConfigurationJson!.Should().Contain("sg-spicegarden1-png");

        var emberOak = templates.Single(t => t.TemplateKey == "ember-oak");

        emberOak.DesignConfigurationJson!.Should().Contain("\"assets\"");
        emberOak.DesignConfigurationJson!.Should().Contain("ember-oak-ember1-png");

        var bloomCafe = templates.Single(t => t.TemplateKey == "bloom-cafe");

        bloomCafe.DesignConfigurationJson!.Should().NotContain("\"assets\"");
    }

    [Fact]
    public async Task SeedAsync_ReconcilesAStaleRow_WithoutLosingAdminContent()
    {
        var fx = TestFactory.Create();
        var existing = TestFactory.AddTemplate(
            fx,
            "Spice Garden Renamed",
            "spice-garden",
            designConfigurationJson: LegacySpiceGardenConfiguration);
        existing.Update("Spice Garden Renamed", "Admin description", false);

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var reloaded = await fx.Db.WebsiteTemplates
            .SingleAsync(t => t.TemplateKey == "spice-garden");
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        reloaded.Id.Should().Be(existing.Id);
        reloaded.Name.Should().Be("Spice Garden Renamed");
        reloaded.Description.Should().Be("Admin description");
        reloaded.IsActive.Should().BeFalse();

        using var document = System.Text.Json.JsonDocument.Parse(
            reloaded.DesignConfigurationJson!);

        var root = document.RootElement;

        root.GetProperty("assets").GetArrayLength().Should().Be(manifest.Assets.Count);
        root.GetProperty("previewContent").GetProperty("heroTitle").GetString()
            .Should().Be("Admin-edited hero title");
        root.GetProperty("previewContent").GetProperty("heroAssetId").GetString()
            .Should().Be("sg-spicegarden1-png");

        (await fx.Db.WebsiteTemplates.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_DoesNotCauseUnnecessaryWrites()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        fx.Db.ChangeTracker.HasChanges().Should().BeFalse();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        fx.Db.ChangeTracker.HasChanges().Should().BeFalse();
        (await fx.Db.WebsiteTemplates.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_DoesNotAlterTemplatesWithoutManifests_OnRepeatedRuns()
    {
        var fx = TestFactory.Create();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var before = await fx.Db.WebsiteTemplates
            .Where(t => t.TemplateKey == "bloom-cafe")
            .Select(t => t.DesignConfigurationJson)
            .SingleAsync();

        await WebsiteTemplateCatalogSeeder.SeedAsync(fx.Db);

        var after = await fx.Db.WebsiteTemplates
            .Where(t => t.TemplateKey == "bloom-cafe")
            .Select(t => t.DesignConfigurationJson)
            .SingleAsync();

        after.Should().Be(before);
    }

    private const string LegacySpiceGardenConfiguration = """
        {
          "schemaVersion": 1,
          "category": "South Asian / Desi",
          "tags": ["desi"],
          "featured": true,
          "displayOrder": 1,
          "design": {
            "palette": {
              "background": "#FFF8EE",
              "surface": "#FFFFFF",
              "text": "#24352A",
              "mutedText": "#667368",
              "primary": "#24563A",
              "accent": "#D9A441",
              "border": "#E9DFCE"
            },
            "typography": {
              "headingFont": "serif-editorial",
              "bodyFont": "sans-humanist"
            },
            "layout": {
              "header": "centered",
              "hero": "split",
              "menu": "featured-cards",
              "gallery": "masonry",
              "footer": "multi-column",
              "buttons": "solid",
              "corners": "soft"
            },
            "sections": [
              { "type": "hero", "enabled": true, "order": 1 },
              { "type": "gallery", "enabled": true, "order": 2 }
            ]
          },
          "previewContent": {
            "eyebrow": "Admin eyebrow",
            "heroTitle": "Admin-edited hero title",
            "heroDescription": "Admin-edited description.",
            "primaryActionLabel": "Admin action"
          }
        }
        """;

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
