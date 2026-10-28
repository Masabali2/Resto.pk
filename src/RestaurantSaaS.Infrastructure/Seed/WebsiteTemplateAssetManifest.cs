using System.Text;
using System.Text.Json;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// One discovered template image. <see cref="AssetId"/> is a stable,
/// deterministic identity derived from the template's asset-ID prefix and the
/// normalized file name; it is deliberately independent from <see cref="Url"/>
/// so the public URL can change without changing the identity stored in the
/// template configuration.
/// </summary>
public sealed record WebsiteTemplateAsset(
    string FileName,
    string AssetId,
    string Url,
    string Alt,
    int Width,
    int Height,
    long Bytes,
    string Sha256);

/// <summary>
/// Explicit section assignments of the discovered images. Every slot is
/// optional: a template only declares the image-bearing sections its design
/// actually supports. Gallery order is preserved exactly as declared.
/// </summary>
public sealed record WebsiteTemplateAssetPlacements(
    string? HeroAssetId,
    string? AboutAssetId,
    IReadOnlyList<string> GalleryAssetIds);

/// <summary>
/// The single, versioned manifest schema shared by every website template.
///
/// A manifest describes the images that ship with the Angular client for ONE
/// template (identified by its stable <c>templateKey</c>) plus the reviewed
/// section placements. It is generated from the real files during development
/// (see <c>scripts/generate-template-asset-manifests.ps1</c>), checked in under
/// <c>Seed/Assets</c> and embedded into the Infrastructure assembly, so the
/// backend never reads a developer's frontend checkout at runtime.
///
/// Loading is deterministic and strict: assets are ordered by file name using
/// ordinal comparison, IDs and URLs are derived from data (never random or
/// index-based), and duplicate IDs/URLs, missing metadata, unassigned images or
/// dangling placements fail loudly instead of silently overwriting an entry.
///
/// Templates without images simply have no manifest; their configuration is
/// left byte-for-byte unchanged.
/// </summary>
public sealed class WebsiteTemplateAssetManifest
{
    public const int CurrentSchemaVersion = 1;

    public const int MaxTemplateKeyLength = 100;
    public const int MaxFileNameLength = 150;
    public const int MaxAltLength = 240;
    public const int MaxAssetIdLength = 100;
    public const int MaxAssetUrlLength = 300;
    public const int MaxImageDimension = 20_000;
    public const int MaxAssets = 32;
    public const int MaxGalleryAssets = 12;

    public const string AssetRoot = "assets/";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".avif",
        ".gif"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private WebsiteTemplateAssetManifest(
        string templateKey,
        string assetIdPrefix,
        string directory,
        IReadOnlyList<WebsiteTemplateAsset> assets,
        WebsiteTemplateAssetPlacements placements,
        IReadOnlyList<string> unassignedFileNames)
    {
        TemplateKey = templateKey;
        AssetIdPrefix = assetIdPrefix;
        Directory = directory;
        Assets = assets;
        Placements = placements;
        UnassignedFileNames = unassignedFileNames;
    }

    /// <summary>Stable template key this manifest belongs to.</summary>
    public string TemplateKey { get; }

    /// <summary>
    /// Prefix used for generated asset IDs. Defaults to <see cref="TemplateKey"/>
    /// but may be overridden (for example Spice Garden keeps its historical
    /// <c>sg-</c> IDs so persisted references never change).
    /// </summary>
    public string AssetIdPrefix { get; }

    /// <summary>Image directory relative to the Angular public root.</summary>
    public string Directory { get; }

    /// <summary>Discoverable assets, ordered by file name (ordinal).</summary>
    public IReadOnlyList<WebsiteTemplateAsset> Assets { get; }

    public WebsiteTemplateAssetPlacements Placements { get; }

    /// <summary>
    /// Images that were reviewed and deliberately left out of every slot.
    /// Listing them here is required: an image can never disappear silently.
    /// </summary>
    public IReadOnlyList<string> UnassignedFileNames { get; }

    /// <summary>
    /// Deterministic asset identity:
    /// <c>{assetIdPrefix}-{lower-cased file name with runs of unsupported
    /// characters collapsed to a single dash}</c>. For example
    /// <c>sg</c> + <c>spicegarden1.png</c> becomes <c>sg-spicegarden1-png</c>.
    /// </summary>
    public static string BuildAssetId(string assetIdPrefix, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetIdPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var builder = new StringBuilder(assetIdPrefix.Trim().ToLowerInvariant());
        builder.Append('-');

        var previousWasDash = false;

        foreach (var c in Path.GetFileName(fileName).Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    /// <summary>
    /// Builds the browser URL from the manifest directory and file name. The
    /// result is always a same-origin, root-relative path; each segment is
    /// percent-encoded so directory characters that are not URL-safe (for
    /// example the ampersand in <c>Ember&amp;Oak</c>) never leak into the
    /// configuration as raw URL syntax.
    /// </summary>
    public static string BuildUrl(string directory, string fileName)
    {
        var escapedDirectory = string.Join(
            '/',
            directory.Split('/').Select(Uri.EscapeDataString));

        return $"/{escapedDirectory}/{Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// Parses and validates a manifest document. Exposed for the generator's
    /// verification step and for tests; production code goes through
    /// <see cref="WebsiteTemplateAssetManifestRegistry"/>.
    /// </summary>
    public static WebsiteTemplateAssetManifest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        ManifestDocument document;

        try
        {
            document = JsonSerializer.Deserialize<ManifestDocument>(json, SerializerOptions)
                ?? throw Invalid("the document is empty.");
        }
        catch (JsonException ex)
        {
            throw Invalid($"the document is not valid JSON ({ex.Message}).");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw Invalid(
                $"schemaVersion must be {CurrentSchemaVersion}, but was {document.SchemaVersion}.");
        }

        if (!IsValidSlug(document.TemplateKey, MaxTemplateKeyLength))
            throw Invalid("'templateKey' must be a lower-case slug (letters, digits and '-' ).");

        var templateKey = document.TemplateKey!;

        var assetIdPrefix = string.IsNullOrWhiteSpace(document.AssetIdPrefix)
            ? templateKey
            : document.AssetIdPrefix.Trim().ToLowerInvariant();

        if (!IsValidSlug(assetIdPrefix, MaxAssetIdLength))
            throw Invalid("'assetIdPrefix' must be a lower-case slug (letters, digits and '-' ).");

        if (!IsValidDirectory(document.Directory))
        {
            throw Invalid(
                $"'directory' must be a same-origin relative directory under '{AssetRoot}'.");
        }

        var directory = document.Directory!;

        var entries = document.Assets ?? throw Invalid("'assets' is required.");

        if (entries.Count == 0)
            throw Invalid("'assets' must contain at least one discovered image.");

        if (entries.Count > MaxAssets)
            throw Invalid($"'assets' may declare at most {MaxAssets} images.");

        var assets = new List<WebsiteTemplateAsset>(entries.Count);
        var assetIds = new HashSet<string>(StringComparer.Ordinal);
        var assetUrls = new HashSet<string>(StringComparer.Ordinal);
        var fileNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FileName) || !IsValidFileName(entry.FileName))
            {
                throw Invalid(
                    $"'{entry.FileName}' is not a supported top-level image file name.");
            }

            var fileName = entry.FileName!;

            if (!fileNames.Add(fileName))
                throw Invalid($"'{fileName}' is declared more than once.");

            var assetId = BuildAssetId(assetIdPrefix, fileName);

            if (assetId.Length is 0 or > MaxAssetIdLength || !IsValidSlug(assetId, MaxAssetIdLength))
                throw Invalid($"'{fileName}' produces an invalid asset ID '{assetId}'.");

            if (!assetIds.Add(assetId))
                throw Invalid($"asset ID collision: '{assetId}' is produced by more than one file.");

            var url = BuildUrl(directory, fileName);

            if (url.Length > MaxAssetUrlLength)
                throw Invalid($"'{fileName}' produces a URL longer than {MaxAssetUrlLength} characters.");

            if (!assetUrls.Add(url))
                throw Invalid($"asset URL collision: '{url}' is produced by more than one file.");

            var alt = entry.Alt?.Trim() ?? string.Empty;

            if (alt.Length is 0 or > MaxAltLength)
                throw Invalid($"'{fileName}' must declare alternative text (1-{MaxAltLength} characters).");

            if (entry.Width is < 1 or > MaxImageDimension ||
                entry.Height is < 1 or > MaxImageDimension)
            {
                throw Invalid($"'{fileName}' must declare image dimensions between 1 and {MaxImageDimension}.");
            }

            if (entry.Bytes <= 0)
                throw Invalid($"'{fileName}' must declare a positive file size.");

            if (!IsSha256(entry.Sha256))
                throw Invalid($"'{fileName}' must declare a lower-case SHA-256 checksum.");

            assets.Add(new WebsiteTemplateAsset(
                fileName,
                assetId,
                url,
                alt,
                entry.Width,
                entry.Height,
                entry.Bytes,
                entry.Sha256!));
        }

        assets.Sort((left, right) => string.CompareOrdinal(left.FileName, right.FileName));

        var byFileName = assets.ToDictionary(asset => asset.FileName, StringComparer.Ordinal);

        var placements = document.Placements ?? throw Invalid("'placements' is required.");

        var hero = ResolvePlacement(placements.Hero, byFileName, "hero");

        var about = ResolvePlacement(placements.About, byFileName, "about");

        var galleryFileNames = placements.Gallery ?? [];

        if (galleryFileNames.Count > MaxGalleryAssets)
            throw Invalid($"'placements.gallery' may reference at most {MaxGalleryAssets} images.");

        var gallery = new List<string>(galleryFileNames.Count);
        var seenGalleryFileNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fileName in galleryFileNames)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw Invalid("'placements.gallery' cannot contain an empty file name.");

            var assetId = ResolvePlacement(fileName, byFileName, "gallery")
                ?? throw Invalid("gallery placement is required.");

            if (!seenGalleryFileNames.Add(fileName!))
                throw Invalid($"gallery placement references '{fileName}' more than once.");

            gallery.Add(assetId);
        }

        if (hero is null && about is null && gallery.Count == 0)
        {
            throw Invalid(
                "at least one image must be assigned to hero, about or gallery (or the manifest should be removed).");
        }

        var referencedFileNames = new HashSet<string>(StringComparer.Ordinal);

        if (placements.Hero is not null)
            referencedFileNames.Add(placements.Hero);

        if (placements.About is not null)
            referencedFileNames.Add(placements.About);

        foreach (var fileName in galleryFileNames)
            referencedFileNames.Add(fileName!);

        var unassigned = new List<string>();
        var seenUnassigned = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fileName in document.Unassigned ?? [])
        {
            if (string.IsNullOrWhiteSpace(fileName) || !byFileName.ContainsKey(fileName))
            {
                throw Invalid(
                    $"'unassigned' references '{fileName}', which is not in the manifest.");
            }

            if (!seenUnassigned.Add(fileName))
                throw Invalid($"'unassigned' references '{fileName}' more than once.");

            if (referencedFileNames.Contains(fileName))
                throw Invalid($"'{fileName}' is both placed and listed as unassigned.");

            unassigned.Add(fileName);
        }

        var unreviewed = assets
            .Where(asset => !referencedFileNames.Contains(asset.FileName) &&
                            !seenUnassigned.Contains(asset.FileName))
            .Select(asset => asset.FileName)
            .ToList();

        if (unreviewed.Count > 0)
        {
            throw Invalid(
                $"every image needs an explicit placement decision; unassigned: {string.Join(", ", unreviewed)}.");
        }

        return new WebsiteTemplateAssetManifest(
            templateKey,
            assetIdPrefix,
            directory,
            assets,
            new WebsiteTemplateAssetPlacements(hero, about, gallery),
            unassigned);
    }

    /// <summary>True when the value is a lower-case slug within the limit.</summary>
    public static bool IsValidSlug(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
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
    /// A directory is safe when it is a relative path under the asset root with
    /// no scheme, no backslashes and no traversal segments.
    /// </summary>
    public static bool IsValidDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxAssetUrlLength ||
            !value.StartsWith(AssetRoot, StringComparison.Ordinal) ||
            value.EndsWith('/') ||
            value.Contains('\\') ||
            value.Contains(':') ||
            value.Contains('?') ||
            value.Contains('#') ||
            value.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = value.Split('/');

        return segments.All(segment =>
            segment.Length > 0 &&
            segment is not "." and not ".." &&
            segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '&'));
    }

    private static string? ResolvePlacement(
        string? fileName,
        IReadOnlyDictionary<string, WebsiteTemplateAsset> assets,
        string placement)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (!assets.TryGetValue(fileName, out var asset))
            throw Invalid($"'{placement}' placement references '{fileName}', which is not in the manifest.");

        return asset.AssetId;
    }

    private static bool IsValidFileName(string fileName)
    {
        if (fileName.Length is 0 or > MaxFileNameLength)
            return false;

        if (fileName.StartsWith('.') ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName.Contains(' '))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);

        if (!AllowedExtensions.Contains(extension))
            return false;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        return nameWithoutExtension.Length > 0 &&
               nameWithoutExtension.All(c =>
                   char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static InvalidOperationException Invalid(string reason) =>
        new($"Template asset manifest is invalid: {reason}");

    private sealed record ManifestDocument(
        int SchemaVersion,
        string? TemplateKey,
        string? AssetIdPrefix,
        string? Directory,
        List<ManifestAsset>? Assets,
        ManifestPlacements? Placements,
        List<string>? Unassigned);

    private sealed record ManifestAsset(
        string? FileName,
        string? Alt,
        int Width,
        int Height,
        long Bytes,
        string? Sha256);

    private sealed record ManifestPlacements(
        string? Hero,
        string? About,
        List<string>? Gallery);
}
