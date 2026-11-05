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
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Context-aware image upload and secure file delivery.
/// TenantId is never accepted from the client; it is resolved from the
/// authenticated session on every request.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class AssetsController : ControllerBase
{
    private readonly IAssetUploadService _upload;
    private readonly IFileStorageService _storage;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantMembershipService _membershipService;

    public AssetsController(
        IAssetUploadService upload,
        IFileStorageService storage,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ITenantMembershipService membershipService)
    {
        _upload = upload;
        _storage = storage;
        _db = db;
        _currentUser = currentUser;
        _membershipService = membershipService;
    }

    /// <summary>Uploads an image and associates it with the authorized destination.</summary>
    [HttpPost("assets/upload")]
    [Authorize(Policy = IdentityPolicyNames.TenantMember)]
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
    /// Secure file delivery. Authorization is bound to the file's owning tenant
    /// and the caller's ACTIVE membership in that tenant — not to the ambient
    /// tenant context or a client-supplied header. Native image requests cannot
    /// send custom headers, so the tenant is resolved from the file metadata and
    /// the authenticated user's memberships.
    /// </summary>
    [HttpGet("files/{*storageKey}")]
    public async Task<IActionResult> GetFile(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return NotFound();

        // Upload and presigned URLs escape the storage key (for example the
        // path separators become %2F). Route values are not always decoded by
        // the host, so normalize before looking the file up.
        var lookupKey = Uri.UnescapeDataString(storageKey);

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        // Resolve by immutable file id or legacy storage key. The storage key
        // form is retained so existing presigned/preview URLs keep working.
        //
        // Tenant filters are disabled here on purpose: authorization is bound to
        // the file's OWNING tenant (checked below via membership), not to the
        // ambient tenant context. Native image requests carry no X-Tenant-Id,
        // so the ambient tenant is often null for multi-tenant users and must
        // not gate the file lookup.
        var previous = _db.DisableTenantFilters;
        _db.DisableTenantFilters = true;
        FileMetadata? file;
        try
        {
            file = Guid.TryParse(lookupKey, out var fileId)
                ? await _db.Files.FirstOrDefaultAsync(
                    f => f.Id == fileId && f.DeletedAt == null,
                    cancellationToken)
                : await _db.Files.FirstOrDefaultAsync(
                    f => f.StorageKey == lookupKey && f.DeletedAt == null,
                    cancellationToken);
        }
        finally
        {
            _db.DisableTenantFilters = previous;
        }

        if (file is null)
            return NotFound();

        // The file must belong to a valid tenant.
        if (file.TenantId is null)
            return NotFound();

        // The caller must hold an ACTIVE membership in the file-owning tenant.
        // This works for users with multiple memberships without an X-Tenant-Id
        // header and never trusts a client-supplied tenant.
        var membership = await _membershipService.GetMembershipAsync(
            userId.Value, file.TenantId.Value, cancellationToken);
        if (membership is null || membership.Status != (int)MembershipStatus.Active)
            return Forbid();

        // Section settings and branding references store the immutable file id;
        // the physical storage key is resolved from the tracked file record.
        // Metadata can outlive the physical bytes (for example after an
        // out-of-band storage cleanup), so a missing file is reported as 404
        // instead of leaking an exception as 500.
        Stream stream;
        try
        {
            stream = await _storage.ReadAsync(file.StorageKey, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }

        // Render inline so the response can be used directly by an <img>
        // element instead of being treated as a file download.
        Response.Headers.ContentDisposition = "inline";
        return File(stream, file.ContentType, enableRangeProcessing: true);
    }

    private Guid? GetUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            return null;
        return userId;
    }

    /// <summary>
    /// Soft-deletes an asset when it is no longer referenced. Returns 409 when
    /// the asset is still referenced by another resource.
    /// </summary>
    [HttpDelete("assets/{id}")]
    [Authorize(Policy = IdentityPolicyNames.TenantMember)]
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
            parsed = UploadDestinationJson.Deserialize(destination);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new DomainException("UPLOAD_INVALID_DESTINATION",
                "Destination is not valid JSON or does not specify a supported destination kind.");
        }
        if (parsed is null)
            throw new DomainException("UPLOAD_INVALID_DESTINATION", "Destination could not be parsed.");

        return parsed;
    }
}
