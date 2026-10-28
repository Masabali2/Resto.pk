using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RestaurantSaaS.Application.Common;
using RestaurantSaaS.Infrastructure.Seed;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// The one generic builder merges reviewed manifests with the authoritative
/// base configuration. These tests pin that only image fields change, that
/// admin edits survive reconciliation, and that every generated document passes
/// the existing Application-layer schema validator.
/// </summary>
public sealed class WebsiteTemplateAssetConfigurationBuilderTests
{
    private const string BaseConfiguration = """
        {
          "schemaVersion": 1,
          "category": "Test / Template",
          "tags": ["test", "reviewed"],
          "featured": false,
          "displayOrder": 42,
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
              { "type": "header", "enabled": true, "order": 1 },
              { "type": "hero", "enabled": true, "order": 2 },
              { "type": "menu", "enabled": true, "order": 3 },
              { "type": "about", "enabled": true, "order": 4 },
              { "type": "gallery", "enabled": true, "order": 5 },
              { "type": "contact", "enabled": true, "order": 6 },
              { "type": "footer", "enabled": true, "order": 7 }
            ]
          },
          "previewContent": {
            "eyebrow": "Reviewed eyebrow",
            "heroTitle": "Admin-edited hero title",
            "heroDescription": "Admin-edited description.",
            "primaryActionLabel": "Reviewed action",
            "sampleDishes": [
              { "name": "Reviewed dish", "price": "PKR 1,000" }
            ]
          }
        }
        """;

    [Fact]
    public void Apply_ReturnsTheBaseConfigurationUnchanged_WhenATemplateHasNoManifest()
    {
        WebsiteTemplateAssetConfigurationBuilder
            .Apply("bloom-cafe", BaseConfiguration)
            .Should()
            .Be(BaseConfiguration);
    }

    [Theory]
    [InlineData("spice-garden")]
    [InlineData("ember-oak")]
    [InlineData("urban-slice")]
    [InlineData("coastal-table")]
    public void Apply_ProducesAValidConfigurationForEveryManifestTemplate(string templateKey)
    {
        var json = WebsiteTemplateAssetConfigurationBuilder.Apply(templateKey, BaseConfiguration);

        WebsiteTemplateDesignSchema.Validate(json).Should().BeNull();

        var manifest = WebsiteTemplateAssetManifestRegistry.Find(templateKey)!;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("category").GetString().Should().Be("Test / Template");
        root.GetProperty("displayOrder").GetInt32().Should().Be(42);

        var previewContent = root.GetProperty("previewContent");
        previewContent.GetProperty("heroTitle").GetString().Should().Be("Admin-edited hero title");

        var assets = root.GetProperty("assets").EnumerateArray().ToList();
        assets.Should().HaveCount(manifest.Assets.Count);
        assets.Select(asset => asset.GetProperty("assetId").GetString())
            .Should().Equal(manifest.Assets.Select(asset => asset.AssetId));

        previewContent.GetProperty("heroAssetId").GetString()
            .Should().Be(manifest.Placements.HeroAssetId);
        previewContent.GetProperty("aboutAssetId").GetString()
            .Should().Be(manifest.Placements.AboutAssetId);
        previewContent.GetProperty("galleryAssetIds").EnumerateArray()
            .Select(entry => entry.GetString())
            .Should().Equal(manifest.Placements.GalleryAssetIds);
    }

    [Fact]
    public void Build_WritesOnlyTheImageFields()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        var json = WebsiteTemplateAssetConfigurationBuilder.Build(BaseConfiguration, manifest);

        var baseRoot = JsonNode.Parse(BaseConfiguration)!.AsObject();
        var resultRoot = JsonNode.Parse(json)!.AsObject();

        resultRoot.Select(pair => pair.Key).Should().BeEquivalentTo(
            baseRoot.Select(pair => pair.Key).Append("assets"));

        var basePreview = baseRoot["previewContent"]!.AsObject();
        var resultPreview = resultRoot["previewContent"]!.AsObject();

        resultPreview.Select(pair => pair.Key).Should().BeEquivalentTo(
            basePreview.Select(pair => pair.Key).Concat(
                new[] { "heroAssetId", "aboutAssetId", "galleryAssetIds" }));

        resultRoot["design"]!.ToJsonString().Should().Be(baseRoot["design"]!.ToJsonString());
    }

    [Fact]
    public void Reconcile_AddsImagesToALegacyRow_AndPreservesAdminEdits()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        var reconciled = WebsiteTemplateAssetConfigurationBuilder.Reconcile(
            BaseConfiguration,
            manifest);

        reconciled.Should().NotBeNull();
        WebsiteTemplateDesignSchema.Validate(reconciled).Should().BeNull();

        using var document = JsonDocument.Parse(reconciled!);
        var root = document.RootElement;

        root.GetProperty("category").GetString().Should().Be("Test / Template");
        root.GetProperty("previewContent").GetProperty("heroTitle").GetString()
            .Should().Be("Admin-edited hero title");
        root.GetProperty("previewContent").GetProperty("heroAssetId").GetString()
            .Should().Be("sg-spicegarden1-png");
        root.GetProperty("assets").GetArrayLength().Should().Be(8);
    }

    [Fact]
    public void Reconcile_IsIdempotent()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("urban-slice")!;

        var first = WebsiteTemplateAssetConfigurationBuilder.Reconcile(
            BaseConfiguration,
            manifest);
        var second = WebsiteTemplateAssetConfigurationBuilder.Reconcile(first, manifest);

        first.Should().NotBeNull();
        second.Should().Be(first);
    }

    [Fact]
    public void Reconcile_RemovesAnOptionalSlotTheManifestNoLongerDeclares()
    {
        var withImages = WebsiteTemplateAssetConfigurationBuilder.Apply(
            "spice-garden",
            BaseConfiguration);

        var heroOnly = WebsiteTemplateAssetManifest.Parse("""
            {
              "schemaVersion": 1,
              "templateKey": "hero-only",
              "directory": "assets/images/Test",
              "assets": [
                {
                  "fileName": "hero.png",
                  "alt": "A reviewed hero image",
                  "width": 1200,
                  "height": 800,
                  "bytes": 4096,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
              ],
              "placements": { "hero": "hero.png" }
            }
            """);

        var reconciled = WebsiteTemplateAssetConfigurationBuilder.Reconcile(withImages, heroOnly);

        reconciled.Should().NotBeNull();

        using var document = JsonDocument.Parse(reconciled!);
        var previewContent = document.RootElement.GetProperty("previewContent");

        previewContent.TryGetProperty("heroAssetId", out var hero).Should().BeTrue();
        hero.GetString().Should().Be("hero-only-hero-png");
        previewContent.TryGetProperty("aboutAssetId", out _).Should().BeFalse();

        var gallery = previewContent.GetProperty("galleryAssetIds");
        gallery.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Reconcile_ReturnsNull_WhenTheExistingConfigurationIsMissing()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        WebsiteTemplateAssetConfigurationBuilder.Reconcile(null, manifest).Should().BeNull();
        WebsiteTemplateAssetConfigurationBuilder.Reconcile("   ", manifest).Should().BeNull();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    public void Reconcile_ReturnsNull_WhenTheExistingConfigurationCannotBeRead(string existing)
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        WebsiteTemplateAssetConfigurationBuilder.Reconcile(existing, manifest).Should().BeNull();
    }

    [Fact]
    public void Reconcile_LeavesAnInvalidExistingConfigurationUntouched()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        const string invalid = """{"schemaVersion":1,"unsupported":true}""";

        WebsiteTemplateAssetConfigurationBuilder.Reconcile(invalid, manifest).Should().BeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{ not json")]
    public void Build_ThrowsWhenTheBaseConfigurationIsNotAnObject(string baseConfiguration)
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        var act = () => WebsiteTemplateAssetConfigurationBuilder.Build(baseConfiguration, manifest);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("ember-oak")!;

        var first = WebsiteTemplateAssetConfigurationBuilder.Build(BaseConfiguration, manifest);
        var second = WebsiteTemplateAssetConfigurationBuilder.Build(BaseConfiguration, manifest);

        first.Should().Be(second);
    }
}
