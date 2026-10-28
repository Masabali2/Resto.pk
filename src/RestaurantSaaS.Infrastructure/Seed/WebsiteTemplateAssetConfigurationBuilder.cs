using System.Text.Json;
using System.Text.Json.Nodes;
using RestaurantSaaS.Application.Common;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// The single configuration builder for manifest-managed template images.
///
/// It merges the authoritative base design document (palette, typography,
/// layout, sections and preview copy) with the reviewed image manifest:
/// only the reusable <c>assets</c> registry and the hero/About/ordered-gallery
/// references are written. Every unrelated property is copied through
/// untouched, so adding images can never redesign a template.
///
/// The resulting document is always validated with the one Application-layer
/// schema validator, so an invalid manifest fails during seeding instead of
/// persisting an unusable template.
/// </summary>
public static class WebsiteTemplateAssetConfigurationBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Applies the images of <paramref name="templateKey"/> when a manifest
    /// exists. Templates without images are returned byte-for-byte unchanged.
    /// </summary>
    public static string Apply(string templateKey, string baseConfigurationJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseConfigurationJson);

        var manifest = WebsiteTemplateAssetManifestRegistry.Find(templateKey);

        return manifest is null
            ? baseConfigurationJson
            : Build(baseConfigurationJson, manifest);
    }

    /// <summary>
    /// Builds a complete configuration from a base document and a manifest.
    /// Used for newly seeded templates, where the base document is
    /// authoritative for every non-image property.
    /// </summary>
    public static string Build(
        string baseConfigurationJson,
        WebsiteTemplateAssetManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseConfigurationJson);
        ArgumentNullException.ThrowIfNull(manifest);

        var root = ParseRoot(
            baseConfigurationJson,
            $"the base design configuration for '{manifest.TemplateKey}'");

        ApplyImageFields(root, manifest);

        var json = root.ToJsonString(SerializerOptions);
        var error = WebsiteTemplateDesignSchema.Validate(json);

        if (error is not null)
        {
            throw new InvalidOperationException(
                $"The design configuration generated for '{manifest.TemplateKey}' from its " +
                $"asset manifest is invalid: {error}");
        }

        return json;
    }

    /// <summary>
    /// Reconciles the manifest-managed image fields into an EXISTING persisted
    /// configuration without replacing any admin-edited content: copy, theme,
    /// typography, layout and section settings are preserved exactly.
    ///
    /// Returns <c>null</c> when there is nothing safe to reconcile — the stored
    /// value is empty, not an object, or already invalid. In that case the
    /// caller leaves the row untouched rather than destroying platform-admin
    /// data.
    /// </summary>
    public static string? Reconcile(
        string? existingConfigurationJson,
        WebsiteTemplateAssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(existingConfigurationJson))
            return null;

        JsonNode? node;

        try
        {
            node = JsonNode.Parse(existingConfigurationJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is not JsonObject root)
            return null;

        ApplyImageFields(root, manifest);

        var json = root.ToJsonString(SerializerOptions);

        return WebsiteTemplateDesignSchema.Validate(json) is null ? json : null;
    }

    private static JsonObject ParseRoot(string json, string description)
    {
        JsonNode? node;

        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"The {description} is not valid JSON.",
                ex);
        }

        if (node is not JsonObject root)
        {
            throw new InvalidOperationException(
                $"The {description} must be a JSON object.");
        }

        return root;
    }

    private static void ApplyImageFields(
        JsonObject root,
        WebsiteTemplateAssetManifest manifest)
    {
        var assets = new JsonArray();

        foreach (var asset in manifest.Assets)
        {
            assets.Add(new JsonObject
            {
                ["assetId"] = asset.AssetId,
                ["url"] = asset.Url,
                ["alt"] = asset.Alt,
                ["width"] = asset.Width,
                ["height"] = asset.Height
            });
        }

        root["assets"] = assets;

        if (root["previewContent"] is not JsonObject previewContent)
        {
            previewContent = new JsonObject();
            root["previewContent"] = previewContent;
        }

        SetOrRemove(previewContent, "heroAssetId", manifest.Placements.HeroAssetId);
        SetOrRemove(previewContent, "aboutAssetId", manifest.Placements.AboutAssetId);

        var galleryAssetIds = new JsonArray();

        foreach (var assetId in manifest.Placements.GalleryAssetIds)
            galleryAssetIds.Add(assetId);

        previewContent["galleryAssetIds"] = galleryAssetIds;
    }

    private static void SetOrRemove(JsonObject target, string key, string? value)
    {
        if (value is null)
        {
            target.Remove(key);
            return;
        }

        target[key] = value;
    }
}
