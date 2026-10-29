using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurants/{restaurantId:guid}/assets")]
public sealed class RestaurantAssetsController(IRestaurantAssetService assets) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RestaurantAssetDto>>>> List(
        Guid restaurantId,
        CancellationToken ct)
    {
        var result = await assets.ListAsync(restaurantId, ct);
        return Ok(ApiResponse<IReadOnlyList<RestaurantAssetDto>>.Ok(result));
    }

    [HttpPost("{assetType}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantUpdate)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<RestaurantAssetDto>>> Upload(
        Guid restaurantId,
        string assetType,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<RestaurantAssetDto>.Fail(
                "ASSET_FILE_REQUIRED", "A non-empty image file is required."));
        }

        using var stream = file.OpenReadStream();
        var result = await assets.UploadAsync(
            restaurantId, assetType, stream, file.FileName, file.ContentType, ct);

        return Ok(ApiResponse<RestaurantAssetDto>.Ok(result));
    }

    [HttpDelete("{assetId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantUpdate)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid restaurantId,
        Guid assetId,
        CancellationToken ct)
    {
        var deleted = await assets.DeleteAsync(restaurantId, assetId, ct);
        if (!deleted)
        {
            return Conflict(ApiResponse<object>.Fail("ASSET_STILL_REFERENCED",
                "The asset is still referenced by a website configuration and cannot be deleted."));
        }

        return NoContent();
    }

    /// <summary>Reads authoritative storage usage for the restaurant. Quota is unavailable when no policy exists.</summary>
    [HttpGet("storage")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantView)]
    public async Task<ActionResult<ApiResponse<StorageUsageDto>>> Storage(
        Guid restaurantId,
        CancellationToken ct)
    {
        var usage = await assets.GetStorageUsageAsync(restaurantId, ct);
        return Ok(ApiResponse<StorageUsageDto>.Ok(usage));
    }
}
