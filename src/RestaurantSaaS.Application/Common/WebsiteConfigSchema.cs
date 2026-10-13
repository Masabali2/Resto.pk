using System.Text.Json;

namespace RestaurantSaaS.Application.Common;

/// <summary>
/// Allowlist schema validator for website builder configuration JSON.
/// Configuration documents are UNTRUSTED user input: they are stored and later
/// served to anonymous browsers, so raw HTML/JS/arbitrary properties must never
/// pass through. This validator is intentionally dependency-free (Application
/// layer) and uses a forward-only System.Text.Json reader so malformed or
/// oversized documents fail closed without materializing object graphs.
///
/// Accepted document shape (all top-level keys optional unless noted):
/// {
///   "schemaVersion": 1,                      // required, must be <= CurrentSchemaVersion
///   "theme": { "colors": { anyKey: "#rgb|#rrggbb" }, "font": one of FontAllowList },
///   "sections": [ { "type": one of SectionTypeAllowList, ...props } ],  // ordered
///   "settings": { booleans/strings/ints only }
/// }
/// Values that are strings are scanned for unsafe content (script tags,
/// javascript:/data: URLs, on-handlers). File references inside section props
/// must be GUIDs — they are ownership-validated against Files at publish time.
/// </summary>
public static class WebsiteConfigSchema
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxDepth = 24;
    public const int MaxKeys = 500;

    public static readonly IReadOnlySet<string> SectionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hero", "menu-preview", "about", "gallery", "contact", "hours", "map", "footer", "banner", "testimonials"
    };

    public static readonly IReadOnlySet<string> FontFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "system-ui", "serif", "sans-serif", "monospace", "poppins", "inter", "roboto", "playfair-display"
    };

    private static readonly HashSet<string> AllowedTopLevel = new(StringComparer.Ordinal)
    { "schemaVersion", "theme", "sections", "settings" };

    /// <summary>Returns null when valid; otherwise the rejection reason.</summary>
    public static string? Validate(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "Configuration JSON is required.";
        if (json.Length > Domain.Entities.Websites.WebsiteConfiguration.MaxJsonLength)
            return "Configuration exceeds the maximum allowed size.";

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
            return $"Configuration is not well-formed JSON: {ex.Message}";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "Configuration root must be a JSON object.";

            foreach (var prop in doc.RootElement.EnumerateObject())
                if (!AllowedTopLevel.Contains(prop.Name))
                    return $"Unsupported configuration property '{prop.Name}'.";

            if (!doc.RootElement.TryGetProperty("schemaVersion", out var sv) ||
                sv.ValueKind != JsonValueKind.Number || !sv.TryGetInt32(out var version) ||
                version < 1 || version > CurrentSchemaVersion)
                return $"'schemaVersion' is required and must be between 1 and {CurrentSchemaVersion}.";

            if (doc.RootElement.TryGetProperty("theme", out var theme))
            {
                if (theme.ValueKind != JsonValueKind.Object) return "'theme' must be an object.";
                if (theme.TryGetProperty("colors", out var colors))
                {
                    if (colors.ValueKind != JsonValueKind.Object) return "'theme.colors' must be an object.";
                    foreach (var c in colors.EnumerateObject())
                        if (c.Value.ValueKind != JsonValueKind.String || !IsHexColor(c.Value.GetString()!))
                            return $"Color '{c.Name}' must be a #rgb or #rrggbb hex value.";
                }
                if (theme.TryGetProperty("font", out var font))
                {
                    var fname = font.ValueKind == JsonValueKind.String ? font.GetString() : null;
                    if (fname is null || !FontFamilies.Contains(fname))
                        return $"'theme.font' must be one of: {string.Join(", ", FontFamilies)}.";
                }
                if (theme.TryGetProperty("customCss", out _) || theme.TryGetProperty("html", out _))
                    return "Raw CSS/HTML overrides are not permitted.";
            }

            if (doc.RootElement.TryGetProperty("sections", out var sections))
            {
                if (sections.ValueKind != JsonValueKind.Array) return "'sections' must be an array.";
                if (sections.GetArrayLength() > 50) return "A website may contain at most 50 sections.";
                var idx = 0;
                foreach (var s in sections.EnumerateArray())
                {
                    idx++;
                    if (s.ValueKind != JsonValueKind.Object) return $"Section #{idx} must be an object.";
                    if (!s.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                        return $"Section #{idx} requires a string 'type'.";
                    var t = type.GetString()!;
                    if (!SectionTypes.Contains(t))
                        return $"Section type '{t}' is not supported.";
                    var err = ScanForUnsafe(s, $"section #{idx}", 0, new int[1]);
                    if (err is not null) return err;
                }
            }

            if (doc.RootElement.TryGetProperty("settings", out var settings))
            {
                if (settings.ValueKind != JsonValueKind.Object) return "'settings' must be an object.";
                var err = ScanForUnsafe(settings, "settings", 0, new int[1]);
                if (err is not null) return err;
            }
        }

        return null; // valid
    }

    private static bool IsHexColor(string value) =>
        value.Length is 4 or 7 && value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    /// <summary>
    /// Recursively rejects deep/oversized documents and scans every string for
    /// injection vectors. Strings are accepted only as plain text; anything that
    /// looks like markup, script URLs or event handlers fails closed.
    /// </summary>
    private static string? ScanForUnsafe(JsonElement el, string path, int depth, int[] keyCount)
    {
        if (depth > MaxDepth) return $"Configuration nesting too deep at {path}.";

        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    if (++keyCount[0] > MaxKeys) return "Configuration contains too many properties.";
                    if (p.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) &&
                        p.Name.Length > 2 && char.IsLetter(p.Name[2]))
                        return $"Event-handler property '{p.Name}' is not permitted at {path}.";
                    var err = ScanForUnsafe(p.Value, $"{path}.{p.Name}", depth + 1, keyCount);
                    if (err is not null) return err;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    var err = ScanForUnsafe(item, $"{path}[]", depth + 1, keyCount);
                    if (err is not null) return err;
                }
                break;

            case JsonValueKind.String:
                var s = el.GetString()!;
                if (ContainsUnsafeText(s))
                    return $"Value at {path} contains disallowed HTML/script content.";
                break;
        }

        return null;
    }

    private static bool ContainsUnsafeText(string value) =>
        value.Contains('<') || value.Contains('>') ||
        value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("vbscript:", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("data:text/html", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("&#", StringComparison.Ordinal); // HTML entities

    /// <summary>Extracts GUID-looking values from section props for file-ownership validation at publish time.</summary>
    public static IEnumerable<Guid> EnumerateFileReferences(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var s in sections.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in s.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(p.Value.GetString(), out var g))
                    yield return g;

                if (p.Value.ValueKind == JsonValueKind.Array)
                    foreach (var item in p.Value.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String &&
                            Guid.TryParse(item.GetString(), out var gi))
                            yield return gi;
            }
        }
    }
}
