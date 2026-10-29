using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Context-aware image upload and secure file delivery.
/// TenantId is never accepted from the client; it is resolved from the
/// authenticated session on every request.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = IdentityPolicyNames.TenantMember)]
public sealed class AssetsController : ControllerBase
{
    private readonly IAssetUploadService _upload;
    private readonly IFileStorageService _storage;
    private readonly ApplicationDbContext _db;

    public AssetsController(
        IAssetUploadService upload,
        IFileStorageService storage,
        ApplicationDbContext db)
    {
        _upload = upload;
        _storage = storage;
        _db = db;
    }

    /// <summary>Uploads an image and associates it with the authorized destination.</summary>
    [HttpPost("assets/upload")]
    public async Task<IActionResult> Upload([FromForm] UploadImageRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.File == null)
            return BadRequest(ApiResponse<object>.Fail("UPLOAD_FILE_REQUIRED",
                "An image file is required."));

        UploadDestination destination;
        try
        {
            destination = ParseDestination(request.Destination);
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message));
        }

        AssetUploadResult result;
        try
        {
            result = await _upload.UploadAsync(destination, request.File, cancellationToken);
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message));
        }

        return Created(string.Empty, ApiResponse<AssetUploadResult>.Ok(result));
    }

    /// <summary>
    /// Secure, tenant-scoped file delivery for editor previews. The caller
    /// must be a member of the tenant that owns the file.
    /// </summary>
    [HttpGet("files/{*storageKey}")]
    public async Task<IActionResult> GetFile(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return NotFound();

        var tenantId = GetTenantId();
        var file = await _db.Files
            .FirstOrDefaultAsync(f =>
                f.StorageKey == storageKey && f.TenantId == tenantId && !f.IsDeleted,
                cancellationToken);

        if (file is null)
            return NotFound();

        var stream = await _storage.ReadAsync(storageKey, cancellationToken);
        return File(stream, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Soft-deletes an asset when it is no longer referenced. Returns 409 when
    /// the asset is still referenced by another resource.
    /// </summary>
    [HttpDelete("assets/{id}")]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _upload.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            // Distinguish "not found" from "still referenced" only loosely; a
            // 409 signals the client should not force removal.
            var exists = await _db.Files.AnyAsync(f => f.Id == id, cancellationToken);
            if (!exists) return NotFound();
            return Conflict(ApiResponse<object>.Fail("ASSET_STILL_REFERENCED",
                "The asset is still referenced and cannot be deleted."));
        }

        return NoContent();
    }

    private static UploadDestination ParseDestination(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new DomainException("UPLOAD_DESTINATION_REQUIRED",
                "An upload destination is required.");

        UploadDestination? parsed;
        try
        {
            parsed = System.Text.Json.JsonSerializer.Deserialize<UploadDestination>(destination,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (System.Text.Json.JsonException)
        {
            throw new DomainException("UPLOAD_INVALID_DESTINATION", "Destination is not valid JSON.");
        }

        if (parsed is null)
            throw new DomainException("UPLOAD_INVALID_DESTINATION", "Destination could not be parsed.");

        return parsed;
    }

    private Guid GetTenantId()
    {
        // TenantMember policy guarantees a tenant has been resolved by the
        // tenant middleware before the controller runs.
        var context = HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        return context.GetRequiredTenantId();
    }
}
