using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using RestaurantSaaS.Infrastructure.Seed;

namespace RestaurantSaaS.Infrastructure.Tests;

/// <summary>
/// The image inventories are generated from the real files and embedded in the
/// Infrastructure assembly. These tests pin the deterministic identity rules,
/// collision detection, explicit placement decisions and — when the frontend
/// checkout is available next to this repository — the actual file bytes.
/// </summary>
public sealed class WebsiteTemplateAssetManifestTests
{
    [Fact]
    public void Registry_LoadsAManifestForEveryTemplateThatShipsImages()
    {
        WebsiteTemplateAssetManifestRegistry.All
            .Select(manifest => manifest.TemplateKey)
            .Should()
            .Equal("coastal-table", "ember-oak", "spice-garden", "urban-slice");
    }

    [Fact]
    public void Registry_ReturnsNullForTemplatesWithoutImages()
    {
        WebsiteTemplateAssetManifestRegistry.Find("bloom-cafe").Should().BeNull();
        WebsiteTemplateAssetManifestRegistry.Find("olive-grove").Should().BeNull();
        WebsiteTemplateAssetManifestRegistry.Find(null).Should().BeNull();
    }

    [Theory]
    [InlineData("spice-garden")]
    [InlineData("ember-oak")]
    [InlineData("urban-slice")]
    [InlineData("coastal-table")]
    public void EveryManifest_IsSelfConsistent(string templateKey)
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find(templateKey);

        manifest.Should().NotBeNull();

        manifest!.Assets.Should().NotBeEmpty();
        manifest.Assets.Select(asset => asset.FileName).Should().BeInAscendingOrder(StringComparer.Ordinal);
        manifest.Assets.Select(asset => asset.AssetId).Should().OnlyHaveUniqueItems();
        manifest.Assets.Select(asset => asset.Url).Should().OnlyHaveUniqueItems();

        foreach (var asset in manifest.Assets)
        {
            asset.AssetId.Should().Be(
                WebsiteTemplateAssetManifest.BuildAssetId(manifest.AssetIdPrefix, asset.FileName));
            asset.Url.Should().Be(
                WebsiteTemplateAssetManifest.BuildUrl(manifest.Directory, asset.FileName));
            asset.Url.Should().StartWith("/");
            asset.Url.Should().NotContain("://");
            asset.Url.Should().NotContain("..");
            asset.Alt.Should().NotBeNullOrWhiteSpace();
            asset.Alt.Length.Should().BeLessThanOrEqualTo(WebsiteTemplateAssetManifest.MaxAltLength);
            asset.Width.Should().BePositive();
            asset.Height.Should().BePositive();
            asset.Bytes.Should().BePositive();
            asset.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        var ids = manifest.Assets.Select(asset => asset.AssetId).ToHashSet(StringComparer.Ordinal);

        if (manifest.Placements.HeroAssetId is { } hero)
            ids.Should().Contain(hero);

        if (manifest.Placements.AboutAssetId is { } about)
            ids.Should().Contain(about);

        manifest.Placements.GalleryAssetIds.Should().OnlyContain(id => ids.Contains(id));
        manifest.Placements.GalleryAssetIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SpiceGarden_RetainsItsHistoricalIdentifiersAndPlacements()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("spice-garden")!;

        manifest.AssetIdPrefix.Should().Be("sg");
        manifest.Assets.Should().HaveCount(8);
        manifest.Placements.HeroAssetId.Should().Be("sg-spicegarden1-png");
        manifest.Placements.AboutAssetId.Should().Be("sg-spicegarden2-png");
        manifest.Placements.GalleryAssetIds.Should().Equal(
            "sg-spicegarden8-png",
            "sg-spicegarden3-png",
            "sg-spicegarden4-png",
            "sg-spicegarden5-png",
            "sg-spicegarden6-png",
            "sg-spicegarden7-png");
        manifest.Assets.Should().OnlyContain(asset =>
            asset.Url == $"/assets/images/SpiceGarden/{asset.FileName}");
    }

    [Fact]
    public void EmberOak_UsesTheSharedIdentityRule()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("ember-oak")!;

        manifest.AssetIdPrefix.Should().Be("ember-oak");
        manifest.Assets.Should().HaveCount(8);
        manifest.Placements.HeroAssetId.Should().Be("ember-oak-ember1-png");
        manifest.Placements.AboutAssetId.Should().Be("ember-oak-ember7-png");
        manifest.Placements.GalleryAssetIds.Should().Equal(
            "ember-oak-ember2-png",
            "ember-oak-ember3-png",
            "ember-oak-ember4-png",
            "ember-oak-ember5-png",
            "ember-oak-ember6-png",
            "ember-oak-ember8-png");

        // The real directory contains an ampersand, so the browser URL is
        // percent-encoded while the manifest keeps the physical directory.
        manifest.Directory.Should().Be("assets/images/Ember&Oak");
        manifest.Assets.Should().OnlyContain(asset =>
            asset.Url.StartsWith("/assets/images/Ember%26Oak/", StringComparison.Ordinal));
    }

    [Fact]
    public void UrbanSlice_UsesTheSharedIdentityRule()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("urban-slice")!;

        manifest.AssetIdPrefix.Should().Be("urban-slice");
        manifest.Placements.HeroAssetId.Should().Be("urban-slice-urban-slice1-png");
        manifest.Placements.AboutAssetId.Should().Be("urban-slice-urban-slice4-png");
        manifest.Placements.GalleryAssetIds.Should().Equal(
            "urban-slice-urban-slice2-png",
            "urban-slice-urban-slice3-png",
            "urban-slice-urban-slice5-png",
            "urban-slice-urban-slice6-png",
            "urban-slice-urban-slice7-png",
            "urban-slice-urban-slice8-png");
    }

    [Fact]
    public void CoastalTable_UsesTheSharedIdentityRule()
    {
        var manifest = WebsiteTemplateAssetManifestRegistry.Find("coastal-table")!;

        manifest.AssetIdPrefix.Should().Be("coastal-table");
        manifest.Directory.Should().Be("assets/images/Coastel-Table");
        manifest.Placements.HeroAssetId.Should().Be("coastal-table-coastal-table8-png");
        manifest.Placements.AboutAssetId.Should().Be("coastal-table-coastal-table7-png");
        manifest.Placements.GalleryAssetIds.Should().HaveCount(6);
    }

    [Fact]
    public void AssetIds_AreDerivedDeterministicallyFromThePrefixAndFileName()
    {
        WebsiteTemplateAssetManifest.BuildAssetId("sg", "spicegarden1.png")
            .Should().Be("sg-spicegarden1-png");
        WebsiteTemplateAssetManifest.BuildAssetId("sg", "Spice Garden 2.PNG")
            .Should().Be("sg-spice-garden-2-png");
        WebsiteTemplateAssetManifest.BuildAssetId("ember-oak", "ember_1.png")
            .Should().Be("ember-oak-ember-1-png");

        // Repeated calls never depend on enumeration order or runtime state.
        WebsiteTemplateAssetManifest.BuildAssetId("sg", "spicegarden1.png")
            .Should().Be(WebsiteTemplateAssetManifest.BuildAssetId("sg", "spicegarden1.png"));
    }

    [Fact]
    public void BuildUrl_IsRootRelativeAndPercentEncodesTheDirectory()
    {
        WebsiteTemplateAssetManifest.BuildUrl("assets/images/Ember&Oak", "ember1.png")
            .Should().Be("/assets/images/Ember%26Oak/ember1.png");

        WebsiteTemplateAssetManifest.BuildUrl("assets/images/SpiceGarden", "spicegarden1.png")
            .Should().Be("/assets/images/SpiceGarden/spicegarden1.png");
    }

    [Fact]
    public void Parse_RejectsADuplicateAssetId()
    {
        var json = MinimalManifest(
            files: ["hero_1.png", "hero-1.png"],
            hero: "hero_1.png");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*collision*");
    }

    [Fact]
    public void Parse_RejectsAnUnsupportedSchemaVersion()
    {
        var json = MinimalManifest(schemaVersion: 2);

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*schemaVersion*");
    }

    [Fact]
    public void Parse_RejectsMissingAlternativeText()
    {
        var json = MinimalManifest(alt: "   ");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*alternative text*");
    }

    [Fact]
    public void Parse_RejectsAnUnsupportedFileType()
    {
        var json = MinimalManifest(files: ["hero.bmp"], hero: "hero.bmp");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a supported*");
    }

    [Fact]
    public void Parse_RejectsAnUnsafeDirectory()
    {
        var json = MinimalManifest(directory: "assets/../secrets");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'directory'*");
    }

    [Fact]
    public void Parse_RejectsAPlacementThatReferencesAMissingFile()
    {
        var json = MinimalManifest(hero: "hero9.png");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*hero9.png*not in the manifest*");
    }

    [Fact]
    public void Parse_RejectsDuplicateGalleryReferences()
    {
        var json = MinimalManifest(
            files: ["first.png", "second.png"],
            hero: null,
            gallery: ["first.png", "first.png"]);

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*gallery*more than once*");
    }

    [Fact]
    public void Parse_RequiresEveryImageToBePlacedOrExplicitlyUnassigned()
    {
        var json = MinimalManifest(
            files: ["hero.png", "extra.png"],
            hero: "hero.png");

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*explicit placement decision*extra.png*");
    }

    [Fact]
    public void Parse_AcceptsExplicitlyUnassignedImages()
    {
        var json = MinimalManifest(
            files: ["hero.png", "extra.png"],
            hero: "hero.png",
            unassigned: ["extra.png"]);

        var manifest = WebsiteTemplateAssetManifest.Parse(json);

        manifest.UnassignedFileNames.Should().Equal("extra.png");
        manifest.Placements.HeroAssetId.Should().Be("test-template-hero-png");
    }

    [Fact]
    public void Parse_RejectsAnImageThatIsBothPlacedAndUnassigned()
    {
        var json = MinimalManifest(
            files: ["hero.png"],
            hero: "hero.png",
            unassigned: ["hero.png"]);

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*both placed and listed as unassigned*");
    }

    [Fact]
    public void Parse_AllowsOptionalHeroAndAboutSlots()
    {
        var json = MinimalManifest(
            files: ["one.png", "two.png"],
            hero: null,
            about: null,
            gallery: ["two.png"],
            unassigned: ["one.png"]);

        var manifest = WebsiteTemplateAssetManifest.Parse(json);

        manifest.Placements.HeroAssetId.Should().BeNull();
        manifest.Placements.AboutAssetId.Should().BeNull();
        manifest.Placements.GalleryAssetIds.Should().Equal("test-template-two-png");
    }

    [Fact]
    public void Parse_RejectsAManifestWithoutAnyPlacement()
    {
        var json = MinimalManifest(
            files: ["one.png"],
            hero: null,
            unassigned: ["one.png"]);

        var act = () => WebsiteTemplateAssetManifest.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one image*must be assigned*");
    }

    /// <summary>
    /// Compares every manifest with the real frontend checkout when it is
    /// present next to this repository (the normal developer layout). CI that
    /// only checks out the backend simply skips the comparison.
    /// </summary>
    [Fact]
    public void Manifests_MatchTheFrontendImages_WhenTheCheckoutIsAvailable()
    {
        var frontendRoot = FindFrontendRoot();

        if (frontendRoot is null)
            return;

        foreach (var manifest in WebsiteTemplateAssetManifestRegistry.All)
        {
            var directory = Path.Combine(
                frontendRoot,
                "public",
                manifest.Directory.Replace('/', Path.DirectorySeparatorChar));

            Directory.Exists(directory).Should().BeTrue(
                $"the '{manifest.TemplateKey}' image directory must exist");

            var files = Directory
                .GetFiles(directory)
                .Select(path => new FileInfo(path))
                .Where(file => file.Extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".avif" or ".gif")
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .ToList();

            files.Select(file => file.Name).Should().Equal(
                manifest.Assets.Select(asset => asset.FileName),
                $"the '{manifest.TemplateKey}' inventory");

            foreach (var asset in manifest.Assets)
            {
                var file = files.Single(entry => entry.Name == asset.FileName);

                file.Length.Should().Be(asset.Bytes, $"{asset.FileName} byte size");

                using var stream = file.OpenRead();
                var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

                hash.Should().Be(asset.Sha256, $"{asset.FileName} content checksum");
            }
        }
    }

    private static string MinimalManifest(
        int schemaVersion = 1,
        string templateKey = "test-template",
        string directory = "assets/images/Test",
        string? assetIdPrefix = null,
        string[]? files = null,
        string? hero = "hero.png",
        string? about = null,
        string[]? gallery = null,
        string[]? unassigned = null,
        string? alt = "A reviewed test image")
    {
        var document = new Dictionary<string, object?>
        {
            ["schemaVersion"] = schemaVersion,
            ["templateKey"] = templateKey,
            ["directory"] = directory,
            ["assets"] = (files ?? ["hero.png"]).Select(fileName => new Dictionary<string, object?>
            {
                ["fileName"] = fileName,
                ["alt"] = alt,
                ["width"] = 100,
                ["height"] = 100,
                ["bytes"] = 1_000,
                ["sha256"] = new string('a', 64)
            }).ToArray(),
            ["placements"] = new Dictionary<string, object?>
            {
                ["hero"] = hero,
                ["about"] = about,
                ["gallery"] = gallery ?? []
            }
        };

        if (assetIdPrefix is not null)
            document["assetIdPrefix"] = assetIdPrefix;

        if (unassigned is not null)
            document["unassigned"] = unassigned;

        return JsonSerializer.Serialize(document);
    }

    private static string? FindFrontendRoot()
    {
        var configured = Environment.GetEnvironmentVariable("RESTO_PK_FRONTEND_ROOT");

        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.GetFullPath(Path.Combine(
                directory.FullName,
                "..",
                "Resto.pkfrontend"));

            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }
}
