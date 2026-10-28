using System.Text.Json;

namespace RestaurantSaaS.Application.Common;

/// <summary>
/// Allowlist schema validator for the DESIGN configuration of a website
/// template.
///
/// The design configuration is platform-owned DATA that drives a generic
/// renderer on the client. It is therefore treated as untrusted input: it may
/// only contain recognized properties and allow-listed visual values. Raw
/// HTML, JavaScript, CSS, expressions, event handlers and free-form URLs are
/// rejected outright.
///
/// Accepted document shape (only <c>schemaVersion</c> is required):
/// {
///   "schemaVersion": 1,
///   "category": "South Asian / Desi",
///   "tags": ["desi", "family-dining"],
///   "featured": true,
///   "displayOrder": 1,
///   "design": {
///     "palette":  { "background": "#FFF8EE", "surface": "#FFFFFF", "text": "#24352A",
///                   "mutedText": "#667368", "primary": "#24563A", "accent": "#D99027",
///                   "border": "#E9DFCE" },
///     "typography": { "headingFont": "serif-editorial", "bodyFont": "sans-modern" },
///     "layout": { "header": "centered", "hero": "split", "menu": "featured-cards",
///                 "gallery": "masonry", "footer": "multi-column",
///                 "buttons": "solid", "corners": "soft" },
///     "sections": [ { "type": "hero", "enabled": true, "order": 1 } ]
///   },
///   "previewContent": {
///     "eyebrow": "Authentic flavours, warmly served",
///     "heroTitle": "A Taste of Tradition",
///     "heroDescription": "Discover comforting dishes prepared with fresh ingredients.",
///     "primaryActionLabel": "Explore Our Menu",
///     "sampleDishes": [ { "name": "Signature Karahi", "price": "PKR 1,650" } ],
///     "heroAssetId": "sg-sample-hero-png",
///     "aboutAssetId": "sg-sample-about-png",
///     "galleryAssetIds": [ "sg-sample-gallery-png" ]
///   },
///   "assets": [
///     {
///       "assetId": "sg-sample-hero-png",
///       "url": "/assets/images/Sample/sample-hero.png",
///       "alt": "A shared table of seasonal dishes",
///       "width": 1600,
///       "height": 1200
///     }
///   ]
/// }
///
/// Assets are an optional registry of reusable template images. Their URLs
/// must be same-origin, root-relative paths: absolute URLs, protocol-relative
/// URLs, schemes (including <c>javascript:</c>), data URLs and parent-directory
/// traversal are rejected. Placements are references by <c>assetId</c>, so a
/// section never embeds a filename and the gallery keeps its configured order.
/// Documents without an asset registry keep validating exactly as before.
/// </summary>
public static class WebsiteTemplateDesignSchema
{
    public const int CurrentSchemaVersion = 1;

    public const int MaxSections = 12;
    public const int MaxTags = 12;
    public const int MaxSampleDishes = 8;
    public const int MaxStringLength = 240;
    public const int MaxAssets = 32;
    public const int MaxGalleryAssets = 12;
    public const int MaxAssetIdLength = 100;
    public const int MaxAssetUrlLength = 300;
    public const int MaxImageDimension = 20_000;

    public static readonly IReadOnlySet<string> HeadingFonts = new HashSet<string>(StringComparer.Ordinal)
    {
        "serif-editorial",
        "serif-traditional",
        "sans-modern",
        "sans-bold",
        "display-wide"
    };

    public static readonly IReadOnlySet<string> BodyFonts = new HashSet<string>(StringComparer.Ordinal)
    {
        "sans-modern",
        "sans-humanist",
        "serif-editorial",
        "serif-traditional"
    };

    public static readonly IReadOnlySet<string> HeaderLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "centered",
        "left",
        "split"
    };

    public static readonly IReadOnlySet<string> HeroLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "split",
        "centered",
        "image-led",
        "cinematic",
        "asymmetric"
    };

    public static readonly IReadOnlySet<string> MenuLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "featured-cards",
        "compact-cards",
        "editorial-list",
        "grid-cards",
        "list-rows"
    };

    public static readonly IReadOnlySet<string> GalleryLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "masonry",
        "grid",
        "strip",
        "carousel"
    };

    public static readonly IReadOnlySet<string> FooterLayouts = new HashSet<string>(StringComparer.Ordinal)
    {
        "multi-column",
        "simple",
        "centered"
    };

    public static readonly IReadOnlySet<string> ButtonStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "solid",
        "outline",
        "pill"
    };

    public static readonly IReadOnlySet<string> CornerStyles = new HashSet<string>(StringComparer.Ordinal)
    {
        "sharp",
        "soft",
        "rounded"
    };

    public static readonly IReadOnlySet<string> SectionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "header",
        "hero",
        "menu",
        "about",
        "gallery",
        "contact",
        "footer"
    };

    private static readonly IReadOnlySet<string> PaletteKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "background",
        "surface",
        "text",
        "mutedText",
        "primary",
        "accent",
        "border"
    };

    private static readonly HashSet<string> AllowedTopLevel = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "category",
        "tags",
        "featured",
        "displayOrder",
        "design",
        "previewContent",
        "assets"
    };

    private static readonly HashSet<string> AllowedAssetKeys = new(StringComparer.Ordinal)
    {
        "assetId",
        "url",
        "alt",
        "width",
        "height"
    };

    private static readonly HashSet<string> AllowedDesignKeys = new(StringComparer.Ordinal)
    {
        "palette",
        "typography",
        "layout",
        "sections"
    };

    private static readonly HashSet<string> AllowedLayoutKeys = new(StringComparer.Ordinal)
    {
        "header",
        "hero",
        "menu",
        "gallery",
        "footer",
        "buttons",
        "corners"
    };

    private static readonly HashSet<string> AllowedSectionKeys = new(StringComparer.Ordinal)
    {
        "type",
        "enabled",
        "order"
    };

    private static readonly HashSet<string> AllowedPreviewContentKeys = new(StringComparer.Ordinal)
    {
        "eyebrow",
        "heroTitle",
        "heroDescription",
        "primaryActionLabel",
        "sampleDishes",
        "heroAssetId",
        "aboutAssetId",
        "galleryAssetIds"
    };

    private static readonly HashSet<string> AllowedDishKeys = new(StringComparer.Ordinal)
    {
        "name",
        "price"
    };

    /// <summary>Returns null when valid; otherwise the rejection reason.</summary>
    public static string? Validate(string? json)
    {
        if (json is null)
            return null;

        if (string.IsNullOrWhiteSpace(json))
            return "Design configuration JSON is required.";

        if (json.Length > Domain.Entities.Websites.WebsiteTemplate.MaxDesignConfigurationLength)
            return "Design configuration exceeds the maximum allowed size.";

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException ex)
        {
            return $"Design configuration is not well-formed JSON: {ex.Message}";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "Design configuration root must be a JSON object.";

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!AllowedTopLevel.Contains(prop.Name))
                    return $"Unsupported design configuration property '{prop.Name}'.";

                if (IsForbiddenPropertyName(prop.Name))
                    return $"Property '{prop.Name}' is not permitted in a design configuration.";
            }

            if (!doc.RootElement.TryGetProperty("schemaVersion", out var sv) ||
                sv.ValueKind != JsonValueKind.Number || !sv.TryGetInt32(out var version) ||
                version != CurrentSchemaVersion)
            {
                return $"'schemaVersion' is required and must be {CurrentSchemaVersion}.";
            }

            if (doc.RootElement.TryGetProperty("category", out var category))
            {
                if (category.ValueKind != JsonValueKind.String)
                    return "'category' must be a string.";

                var value = category.GetString()!;
                if (value.Length > 80)
                    return "'category' cannot exceed 80 characters.";

                if (ContainsUnsafeText(value))
                    return "'category' contains disallowed content.";
            }

            if (doc.RootElement.TryGetProperty("tags", out var tags))
            {
                if (tags.ValueKind != JsonValueKind.Array)
                    return "'tags' must be an array.";

                if (tags.GetArrayLength() > MaxTags)
                    return $"A template may declare at most {MaxTags} tags.";

                foreach (var tag in tags.EnumerateArray())
                {
                    if (tag.ValueKind != JsonValueKind.String)
                        return "Every tag must be a string.";

                    var value = tag.GetString()!;
                    if (value.Length is 0 or > 40)
                        return "Tags must be between 1 and 40 characters.";

                    if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                        return "Tags may contain only letters, digits, '-' and '_'.";

                    if (ContainsUnsafeText(value))
                        return "A tag contains disallowed content.";
                }
            }

            if (doc.RootElement.TryGetProperty("featured", out var featured) &&
                featured.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return "'featured' must be a boolean.";
            }

            if (doc.RootElement.TryGetProperty("displayOrder", out var displayOrder))
            {
                if (displayOrder.ValueKind != JsonValueKind.Number ||
                    !displayOrder.TryGetInt32(out var order) ||
                    order < 0 || order > 9999)
                {
                    return "'displayOrder' must be an integer between 0 and 9999.";
                }
            }

            if (doc.RootElement.TryGetProperty("design", out var design))
            {
                var designError = ValidateDesign(design);
                if (designError is not null)
                    return designError;
            }

            var assetIds = new HashSet<string>(StringComparer.Ordinal);

            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                var assetsError = ValidateAssets(assets, assetIds);
                if (assetsError is not null)
                    return assetsError;
            }

            if (doc.RootElement.TryGetProperty("previewContent", out var previewContent))
            {
                var previewError = ValidatePreviewContent(previewContent, assetIds);
                if (previewError is not null)
                    return previewError;
            }
        }

        return null;
    }

    private static string? ValidateDesign(JsonElement design)
    {
        if (design.ValueKind != JsonValueKind.Object)
            return "'design' must be an object.";

        foreach (var prop in design.EnumerateObject())
        {
            if (!AllowedDesignKeys.Contains(prop.Name))
                return $"Unsupported design property '{prop.Name}'.";

            if (IsForbiddenPropertyName(prop.Name))
                return $"Property 'design.{prop.Name}' is not permitted.";
        }

        if (design.TryGetProperty("palette", out var palette))
        {
            if (palette.ValueKind != JsonValueKind.Object)
                return "'design.palette' must be an object.";

            foreach (var color in palette.EnumerateObject())
            {
                if (!PaletteKeys.Contains(color.Name))
                    return $"Unsupported palette colour '{color.Name}'.";

                if (color.Value.ValueKind != JsonValueKind.String ||
                    !IsHexColor(color.Value.GetString()!))
                {
                    return $"Palette colour '{color.Name}' must be a #rgb or #rrggbb hex value.";
                }
            }
        }

        if (design.TryGetProperty("typography", out var typography))
        {
            if (typography.ValueKind != JsonValueKind.Object)
                return "'design.typography' must be an object.";

            foreach (var token in typography.EnumerateObject())
            {
                if (token.Name is not ("headingFont" or "bodyFont"))
                    return $"Unsupported typography token '{token.Name}'.";

                if (token.Value.ValueKind != JsonValueKind.String)
                    return $"Typography token '{token.Name}' must be a string.";

                var value = token.Value.GetString()!;
                var allowList = token.Name == "headingFont" ? HeadingFonts : BodyFonts;

                if (!allowList.Contains(value))
                    return $"Typography token '{token.Name}' must be one of: {string.Join(", ", allowList)}.";
            }
        }

        if (design.TryGetProperty("layout", out var layout))
        {
            if (layout.ValueKind != JsonValueKind.Object)
                return "'design.layout' must be an object.";

            foreach (var variant in layout.EnumerateObject())
            {
                if (!AllowedLayoutKeys.Contains(variant.Name))
                    return $"Unsupported layout key '{variant.Name}'.";

                if (variant.Value.ValueKind != JsonValueKind.String)
                    return $"Layout variant '{variant.Name}' must be a string.";

                var value = variant.Value.GetString()!;
                var allowList = LayoutAllowList(variant.Name);

                if (allowList is null)
                    return $"Layout key '{variant.Name}' is not supported.";

                if (!allowList.Contains(value))
                    return $"Layout variant '{variant.Name}' must be one of: {string.Join(", ", allowList)}.";
            }
        }

        if (design.TryGetProperty("sections", out var sections))
        {
            if (sections.ValueKind != JsonValueKind.Array)
                return "'design.sections' must be an array.";

            if (sections.GetArrayLength() > MaxSections)
                return $"A template may declare at most {MaxSections} sections.";

            var index = 0;
            foreach (var section in sections.EnumerateArray())
            {
                index++;
                if (section.ValueKind != JsonValueKind.Object)
                    return $"Section #{index} must be an object.";

                foreach (var prop in section.EnumerateObject())
                {
                    if (!AllowedSectionKeys.Contains(prop.Name))
                        return $"Unsupported section property '{prop.Name}'.";

                    if (IsForbiddenPropertyName(prop.Name))
                        return $"Property 'sections.{prop.Name}' is not permitted.";
                }

                if (!section.TryGetProperty("type", out var type) ||
                    type.ValueKind != JsonValueKind.String)
                {
                    return $"Section #{index} requires a string 'type'.";
                }

                var sectionType = type.GetString()!;
                if (!SectionTypes.Contains(sectionType))
                    return $"Section type '{sectionType}' is not supported.";

                if (section.TryGetProperty("enabled", out var enabled) &&
                    enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return $"Section #{index} 'enabled' must be a boolean.";
                }

                if (section.TryGetProperty("order", out var order))
                {
                    if (order.ValueKind != JsonValueKind.Number ||
                        !order.TryGetInt32(out var value) ||
                        value < 0 || value > 999)
                    {
                        return $"Section #{index} 'order' must be an integer between 0 and 999.";
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates the optional reusable image registry. The <c>url</c> key is the
    /// single exception to the blanket forbidden-property guard: it is allowed
    /// here because every value is constrained to a same-origin, root-relative
    /// path (no scheme, host, traversal or backslash).
    /// </summary>
    private static string? ValidateAssets(JsonElement assets, HashSet<string> assetIds)
    {
        if (assets.ValueKind != JsonValueKind.Array)
            return "'assets' must be an array.";

        if (assets.GetArrayLength() > MaxAssets)
            return $"A template may declare at most {MaxAssets} assets.";

        var urls = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var asset in assets.EnumerateArray())
        {
            index++;

            if (asset.ValueKind != JsonValueKind.Object)
                return $"Asset #{index} must be an object.";

            foreach (var prop in asset.EnumerateObject())
            {
                if (!AllowedAssetKeys.Contains(prop.Name))
                    return $"Unsupported asset property '{prop.Name}'.";

                if (prop.Name != "url" && IsForbiddenPropertyName(prop.Name))
                    return $"Property 'assets.{prop.Name}' is not permitted.";
            }

            if (!asset.TryGetProperty("assetId", out var assetId) ||
                assetId.ValueKind != JsonValueKind.String)
            {
                return $"Asset #{index} requires a string 'assetId'.";
            }

            var id = assetId.GetString()!;

            if (!IsValidAssetId(id))
                return $"Asset #{index} 'assetId' must be a lowercase slug (letters, digits and '-', up to {MaxAssetIdLength} characters).";

            if (!assetIds.Add(id))
                return $"Asset ID '{id}' is declared more than once.";

            if (!asset.TryGetProperty("url", out var url) ||
                url.ValueKind != JsonValueKind.String)
            {
                return $"Asset '{id}' requires a string 'url'.";
            }

            var urlValue = url.GetString()!;

            if (!IsSafeAssetUrl(urlValue))
                return $"Asset '{id}' must use a same-origin, root-relative image URL.";

            if (!urls.Add(urlValue))
                return $"Asset URL '{urlValue}' is declared more than once.";

            if (!asset.TryGetProperty("alt", out var alt) ||
                alt.ValueKind != JsonValueKind.String)
            {
                return $"Asset '{id}' requires a string 'alt'.";
            }

            var altValue = alt.GetString()!.Trim();

            if (altValue.Length is 0 or > MaxStringLength)
                return $"Asset '{id}' 'alt' must be between 1 and {MaxStringLength} characters.";

            if (ContainsUnsafeText(altValue))
                return $"Asset '{id}' 'alt' contains disallowed content.";

            foreach (var dimension in new[] { "width", "height" })
            {
                if (!asset.TryGetProperty(dimension, out var value))
                    continue;

                if (value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetInt32(out var pixels) ||
                    pixels < 1 ||
                    pixels > MaxImageDimension)
                {
                    return $"Asset '{id}' '{dimension}' must be an integer between 1 and {MaxImageDimension}.";
                }
            }
        }

        return null;
    }

    private static string? ValidatePreviewContent(
        JsonElement previewContent,
        IReadOnlySet<string> assetIds)
    {
        if (previewContent.ValueKind != JsonValueKind.Object)
            return "'previewContent' must be an object.";

        foreach (var prop in previewContent.EnumerateObject())
        {
            if (!AllowedPreviewContentKeys.Contains(prop.Name))
                return $"Unsupported preview content property '{prop.Name}'.";

            if (IsForbiddenPropertyName(prop.Name))
                return $"Property 'previewContent.{prop.Name}' is not permitted.";
        }

        foreach (var textKey in new[] { "eyebrow", "heroTitle", "heroDescription", "primaryActionLabel" })
        {
            if (!previewContent.TryGetProperty(textKey, out var text))
                continue;

            if (text.ValueKind != JsonValueKind.String)
                return $"'{textKey}' must be a string.";

            var value = text.GetString()!;
            if (value.Length > MaxStringLength)
                return $"'{textKey}' cannot exceed {MaxStringLength} characters.";

            if (ContainsUnsafeText(value))
                return $"'{textKey}' contains disallowed content.";
        }

        if (previewContent.TryGetProperty("sampleDishes", out var dishes))
        {
            if (dishes.ValueKind != JsonValueKind.Array)
                return "'previewContent.sampleDishes' must be an array.";

            if (dishes.GetArrayLength() > MaxSampleDishes)
                return $"A template may declare at most {MaxSampleDishes} sample dishes.";

            var index = 0;
            foreach (var dish in dishes.EnumerateArray())
            {
                index++;
                if (dish.ValueKind != JsonValueKind.Object)
                    return $"Sample dish #{index} must be an object.";

                foreach (var prop in dish.EnumerateObject())
                {
                    if (!AllowedDishKeys.Contains(prop.Name))
                        return $"Unsupported sample dish property '{prop.Name}'.";

                    if (IsForbiddenPropertyName(prop.Name))
                        return $"Property 'sampleDishes.{prop.Name}' is not permitted.";
                }

                foreach (var textKey in new[] { "name", "price" })
                {
                    if (!dish.TryGetProperty(textKey, out var text))
                        continue;

                    if (text.ValueKind != JsonValueKind.String)
                        return $"Sample dish #{index} '{textKey}' must be a string.";

                    var value = text.GetString()!;
                    if (value.Length > 120)
                        return $"Sample dish #{index} '{textKey}' cannot exceed 120 characters.";

                    if (ContainsUnsafeText(value))
                        return $"Sample dish #{index} '{textKey}' contains disallowed content.";
                }
            }
        }

        foreach (var referenceKey in new[] { "heroAssetId", "aboutAssetId" })
        {
            if (!previewContent.TryGetProperty(referenceKey, out var reference))
                continue;

            if (reference.ValueKind != JsonValueKind.String)
                return $"'{referenceKey}' must be a string.";

            var assetId = reference.GetString()!;

            if (!assetIds.Contains(assetId))
                return $"'{referenceKey}' references unknown asset '{assetId}'.";
        }

        if (previewContent.TryGetProperty("galleryAssetIds", out var gallery))
        {
            if (gallery.ValueKind != JsonValueKind.Array)
                return "'galleryAssetIds' must be an array.";

            if (gallery.GetArrayLength() > MaxGalleryAssets)
                return $"A gallery may reference at most {MaxGalleryAssets} assets.";

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var reference in gallery.EnumerateArray())
            {
                if (reference.ValueKind != JsonValueKind.String)
                    return "Every gallery asset reference must be a string.";

                var assetId = reference.GetString()!;

                if (!assetIds.Contains(assetId))
                    return $"'galleryAssetIds' references unknown asset '{assetId}'.";

                if (!seen.Add(assetId))
                    return $"Gallery asset '{assetId}' is referenced more than once.";
            }
        }

        return null;
    }

    private static bool IsValidAssetId(string value)
    {
        if (value.Length is 0 or > MaxAssetIdLength)
            return false;

        if (!char.IsAsciiLetterOrDigit(value[0]) ||
            !char.IsAsciiLetterOrDigit(value[^1]))
        {
            return false;
        }

        return value.All(c =>
            char.IsAsciiLetterOrDigit(c) ||
            (c >= 'a' && c <= 'z') ||
            c == '-');
    }

    /// <summary>
    /// Accepts only same-origin, root-relative image paths. Absolute URLs
    /// (any scheme), protocol-relative URLs, backslashes, parent-directory
    /// traversal, query strings and fragments are rejected, so an asset can
    /// never point outside the site that serves the renderer.
    /// </summary>
    private static bool IsSafeAssetUrl(string value)
    {
        if (value.Length is 0 or > MaxAssetUrlLength)
            return false;

        if (value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal))
            return false;

        if (value.Contains('\\') ||
            value.Contains(':') ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(c =>
            char.IsAsciiLetterOrDigit(c) ||
            c is '-' or '.' or '_' or '~' or '/' or '%');
    }

    private static IReadOnlySet<string>? LayoutAllowList(string key) => key switch
    {
        "header" => HeaderLayouts,
        "hero" => HeroLayouts,
        "menu" => MenuLayouts,
        "gallery" => GalleryLayouts,
        "footer" => FooterLayouts,
        "buttons" => ButtonStyles,
        "corners" => CornerStyles,
        _ => null
    };

    private static bool IsForbiddenPropertyName(string name) =>
        name.Equals("html", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("customcss", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("css", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("expression", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("url", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("src", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
        (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) &&
         name.Length > 2 &&
         char.IsLetter(name[2]));

    private static bool IsHexColor(string value) =>
        value.Length is 4 or 7 && value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static bool ContainsUnsafeText(string value) =>
        value.Contains('<') || value.Contains('>') ||
        value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("vbscript:", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("data:text/html", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("&#", StringComparison.Ordinal);
}
