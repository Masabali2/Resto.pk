using Microsoft.AspNetCore.Http;
using RestaurantSaaS.Application.Common.Files;

namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Context-aware image upload. The destination (not a user-chosen role)
/// determines how the file is associated. Implementations enforce tenant
/// isolation and resource-level authorization for every destination.
/// </summary>
public interface IAssetUploadService
{
    /// <summary>Validates, stores, and associates an uploaded image.</summary>
    Task<AssetUploadResult> UploadAsync(
        UploadDestination destination,
        IFormFile file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an asset when it is no longer referenced. Returns false when
    /// the asset is still referenced and must not be deleted.
    /// </summary>
    Task<bool> DeleteAsync(Guid assetId, CancellationToken cancellationToken = default);
}
