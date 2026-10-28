using System.Text;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// Loads every embedded <c>*.assets.json</c> manifest exactly once and indexes
/// it by its stable <see cref="WebsiteTemplateAssetManifest.TemplateKey"/>.
///
/// The registry is fully data-driven: adding a template's images only requires
/// adding its manifest file next to the existing ones and adding it to the
/// project's embedded resources. There is no per-template code and no switch
/// statement to extend.
/// </summary>
public static class WebsiteTemplateAssetManifestRegistry
{
    private const string ResourceSuffix = ".assets.json";

    private static readonly Lazy<IReadOnlyDictionary<string, WebsiteTemplateAssetManifest>>
        Manifests = new(LoadAll, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>All manifests, ordered by template key (ordinal).</summary>
    public static IReadOnlyList<WebsiteTemplateAssetManifest> All =>
        Manifests.Value.Values
            .OrderBy(manifest => manifest.TemplateKey, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns the manifest for a template, or <c>null</c> when the template
    /// does not ship images. Templates without a manifest are fully supported.
    /// </summary>
    public static WebsiteTemplateAssetManifest? Find(string? templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            return null;

        return Manifests.Value.TryGetValue(templateKey.Trim(), out var manifest)
            ? manifest
            : null;
    }

    private static IReadOnlyDictionary<string, WebsiteTemplateAssetManifest> LoadAll()
    {
        var assembly = typeof(WebsiteTemplateAssetManifestRegistry).Assembly;

        var resourceNames = assembly
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var manifests = new Dictionary<string, WebsiteTemplateAssetManifest>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded template asset manifest '{resourceName}' could not be opened.");

            using var reader = new StreamReader(stream, Encoding.UTF8);

            var manifest = WebsiteTemplateAssetManifest.Parse(reader.ReadToEnd());

            if (!manifests.TryAdd(manifest.TemplateKey, manifest))
            {
                throw new InvalidOperationException(
                    $"More than one embedded template asset manifest declares templateKey " +
                    $"'{manifest.TemplateKey}' ('{resourceName}').");
            }
        }

        return manifests;
    }
}
