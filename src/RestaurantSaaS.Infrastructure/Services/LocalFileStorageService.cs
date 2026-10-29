using Microsoft.Extensions.Configuration;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private readonly string _root = Path.GetFullPath(
        configuration["Storage:LocalRoot"]
        ?? Path.Combine(Path.GetDirectoryName(typeof(LocalFileStorageService).Assembly.Location) ?? ".", "storage"));

    public async Task<string> SaveAsync(
        Guid? tenantId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10)
            extension = string.Empty;

        var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var key = BuildKey(tenantId, safeName);
        var fullPath = ResolveFullPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);

        return key;
    }

    public async Task<Stream> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storageKey);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Stored file not found.", fullPath);

        await using var source = File.OpenRead(fullPath);
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public string GetPresignedUrl(string storageKey, TimeSpan expiration) =>
        $"/api/v1/files/{Uri.EscapeDataString(storageKey)}?exp={(int)expiration.TotalSeconds}";

    private string BuildKey(Guid? tenantId, string safeFileName) =>
        tenantId is null
            ? $"shared/{safeFileName}"
            : $"tenant/{tenantId:N}/{safeFileName}";

    private string ResolveFullPath(string key)
    {
        var segments = key.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s is not "." and not "..")
            .ToArray();

        var candidate = Path.GetFullPath(Path.Combine([_root, .. segments]));
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSep, StringComparison.Ordinal) && candidate != _root)
            throw new InvalidOperationException("Storage key escapes the configured storage root.");

        return candidate;
    }
}
