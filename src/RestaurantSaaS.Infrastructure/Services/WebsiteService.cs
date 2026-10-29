using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

    ///<summary>
    ///Tenant-scoped website management. Mirrors the established RestaurantService
    ///pattern: tenant scope comes EXCLUSIVELY from ITenantContext, every mutation
    ///re-verifies live membership + permission (never a stale JWT claim), and EF
    ///global query filters are treated as defense-in-depth rather than the only
    ///isolation mechanism (explicit ownership checks on every load).
    ///
    ///Draft/publish lifecycle:
    /// - SaveDraft mutates ONLY the single draft version (website never loses it).
    /// - Publish freezes the draft into an immutable published version inside one
    ///   transaction; anonymous visitors switch atomically to the new content.
    /// - Editing after publish creates a NEW draft; the published version is
    ///   untouched until the next publish.
    /// </summary>
public sealed class WebsiteService(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService) : IWebsiteService
{
    public async Task<IReadOnlyList<WebsiteTemplateDto>> ListAvailableTemplatesAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteView, requireActiveTenant: false, ct);

        var templates = await db.WebsiteTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return templates
            .Select(t => new WebsiteTemplateDto(
                t.Id,
                t.Name,
                t.Description,
                t.TemplateKey,
                t.PreviewImageFileId,
                t.IsActive,
                t.CreatedAt,
                t.DesignConfigurationJson))
            .ToList();
    }

    public async Task<WebsiteDto> CreateAsync(CreateWebsiteRequest request, CancellationToken ct = default)
    {
        var tenantId = await RequirePermissionAsync(Permissions.WebsiteUpdate, requireActiveTenant: true, ct);

        // Restaurant must belong to THIS tenant — explicit check in addition to the
        // composite FK (TenantId, RestaurantId) -> Restaurants(TenantId, Id).
        var restaurant = await db.Restaurants.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RestaurantId && r.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Restaurant", request.RestaurantId);

        // Template must exist AND be active for NEW websites.
        var template = await db.WebsiteTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.WebsiteTemplateId, ct)
            ?? throw new NotFoundException("WebsiteTemplate", request.WebsiteTemplateId);
        if (!template.IsActive)
            throw new DomainException("WEBSITE_RULE", "This template is no longer available for new websites.");

        var slug = Website.NormalizeSlug(request.Slug);

        // Friendly pre-check; the UNIQUE(TenantId, Slug) index is the real guarantee.
        if (await db.Websites.AnyAsync(w => w.TenantId == tenantId && w.Slug == slug, ct))
            throw new ConflictException("A website with this slug already exists in your tenant.");

        var website = Website.Create(tenantId, restaurant.Id, template.Id, request.Name, slug);

        db.Websites.Add(website);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("A website with this slug already exists in your tenant.");
        }

        return await ToDtoAsync(website.Id, ct);
    }

    public async Task<PagedResult<WebsiteDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteView, requireActiveTenant: false, ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Websites.AsNoTracking().OrderBy(w => w.Name);
        var total = await query.CountAsync(ct);
        var ids = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(w => w.Id).ToListAsync(ct);

        var dtos = new List<WebsiteDto>(ids.Count);
        foreach (var id in ids) dtos.Add(await ToDtoAsync(id, ct));
        return new PagedResult<WebsiteDto> { Items = dtos, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<WebsiteDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteView, requireActiveTenant: false, ct);
        await LoadOwnedAsync(id, ct); // ownership validation even for reads
        return await ToDtoAsync(id, ct);
    }

    public async Task<WebsiteDraftDto> GetDraftAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteView, requireActiveTenant: false, ct);
        await LoadOwnedAsync(id, ct); // ownership validation even for reads

        var draft = await db.WebsiteConfigurations
            .AsNoTracking()
            .Where(c => c.WebsiteId == id && c.IsDraft)
            .Select(c => new WebsiteDraftDto(
                c.WebsiteId,
                true,
                c.ConfigurationJson,
                c.ConfigurationVersion))
            .FirstOrDefaultAsync(ct);

        return draft ?? new WebsiteDraftDto(id, false, null, null);
    }

    public async Task<WebsiteDto> UpdateAsync(Guid id, UpdateWebsiteRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteUpdate, requireActiveTenant: true, ct);
        var website = await LoadOwnedAsync(id, ct);

        if (request.WebsiteTemplateId is { } templateId && templateId != website.WebsiteTemplateId)
        {
            var template = await db.WebsiteTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct)
                ?? throw new NotFoundException("WebsiteTemplate", templateId);

            // Inactive templates may not be SELECTED/CHANGED-TO; websites already using
            // an inactive template keep their data (documented Phase-5 behavior).
            if (!template.IsActive)
                throw new DomainException("WEBSITE_RULE", "This template is not available for selection.");
        }

        string? newSlug = null;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            newSlug = Website.NormalizeSlug(request.Slug);
            if (newSlug != website.Slug &&
                await db.Websites.AnyAsync(w => w.TenantId == website.TenantId && w.Slug == newSlug && w.Id != website.Id, ct))
            {
                // Never disclose which tenant owns the conflicting slug.
                throw new ConflictException("This slug is not available.");
            }
        }

        website.UpdateProfile(request.Name, newSlug ?? request.Slug, request.WebsiteTemplateId);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("This slug is not available.");
        }

        return await ToDtoAsync(website.Id, ct);
    }

    public async Task<WebsiteDto> SaveDraftAsync(Guid id, SaveWebsiteDraftRequest request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsiteUpdate, requireActiveTenant: true, ct);
        var website = await LoadOwnedAsync(id, ct);

        // Schema allowlist validation (fail closed on unknown props/scripts/deep nesting).
        var schemaError = WebsiteConfigSchema.Validate(request.ConfigurationJson);
        if (schemaError is not null)
            throw new DomainException("WEBSITE_RULE", schemaError);

        var draft = await db.WebsiteConfigurations
            .FirstOrDefaultAsync(c => c.WebsiteId == website.Id && c.IsDraft, ct);

        if (draft is null)
        {
            var maxVersion = await db.WebsiteConfigurations
                .Where(c => c.WebsiteId == website.Id)
                .Select(c => (int?)c.ConfigurationVersion)
                .MaxAsync(ct) ?? 0;
            draft = WebsiteConfiguration.CreateDraft(website.Id, request.ConfigurationJson, maxVersion + 1);
            db.WebsiteConfigurations.Add(draft);
        }
        else
        {
            draft.UpdateDraft(request.ConfigurationJson);
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(website.Id, ct);
    }

    /// <summary>
    /// Publish = freeze current draft into an immutable published version.
    /// Runs in ONE transaction so a failed publish can never leave a partially
    /// published website. Idempotent-friendly: republishing without draft changes
    /// still produces exactly one non-draft "current" version per website because
    /// the draft row transitions in place (no duplicate versions).
    /// </summary>
    public async Task<WebsiteDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsitePublish, requireActiveTenant: true, ct);
        var website = await LoadOwnedAsync(id, ct);

        var draft = await db.WebsiteConfigurations
            .FirstOrDefaultAsync(c => c.WebsiteId == website.Id && c.IsDraft, ct)
            ?? throw new DomainException("WEBSITE_RULE", "There is no draft configuration to publish.");

        // ---- Pre-publish validation (all fail-closed) ----
        var template = await db.WebsiteTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == website.WebsiteTemplateId, ct)
            ?? throw new DomainException("WEBSITE_RULE", "The selected template no longer exists.");

        var schemaError = WebsiteConfigSchema.Validate(draft.ConfigurationJson);
        if (schemaError is not null)
            throw new DomainException("WEBSITE_RULE", $"Configuration is not publishable: {schemaError}");

        // Referenced files must exist and belong to THIS tenant (or be platform/public).
        var fileRefs = WebsiteConfigSchema.EnumerateFileReferences(draft.ConfigurationJson).Distinct().ToList();
        if (fileRefs.Count > 0)
        {
            var validCount = await db.Files.AsNoTracking().IgnoreQueryFilters()
                .CountAsync(f => fileRefs.Contains(f.Id) && f.DeletedAt == null &&
                                 (f.TenantId == website.TenantId || f.TenantId == null), ct);
            if (validCount != fileRefs.Count)
                throw new DomainException("WEBSITE_RULE", "Configuration references files that do not exist or belong to another tenant.");
        }

        // Restaurant must exist and not be permanently deleted. Closed/suspended
        // restaurants keep their published site reachable (Phase-5 policy, documented).
        var restaurantOk = await db.Restaurants.AsNoTracking()
            .AnyAsync(r => r.Id == website.RestaurantId && !r.IsDeleted, ct);
        if (!restaurantOk)
            throw new DomainException("WEBSITE_RULE", "The associated restaurant cannot be published.");

        var utcNow = DateTime.UtcNow;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                website.Publish(draft, template, utcNow);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        return await ToDtoAsync(website.Id, ct);
    }

    public async Task<WebsiteDto> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(Permissions.WebsitePublish, requireActiveTenant: true, ct);
        var website = await LoadOwnedAsync(id, ct);

        website.Unpublish(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(website.Id, ct);
    }

    // ---------------- helpers ----------------

    private async Task<Guid> RequirePermissionAsync(string permission, bool requireActiveTenant, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(permission))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        if (requireActiveTenant)
        {
            var operational = await db.Tenants.AsNoTracking()
                .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);
            if (!operational)
                throw new DomainException("WEBSITE_RULE", "This tenant is not active.");
        }

        return tenantId;
    }

    private async Task<Website> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        var website = await db.Websites.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Website", id); // filtered read: no existence leakage

        if (website.TenantId != tenantId)
            throw new CrossTenantAccessException();

        return website;
    }

    private async Task<WebsiteDto> ToDtoAsync(Guid websiteId, CancellationToken ct)
    {
        var website = await db.Websites.AsNoTracking()
            .Include(w => w.Restaurant)
            .Include(w => w.Template)
            .FirstAsync(w => w.Id == websiteId, ct);

        var draftVersion = await db.WebsiteConfigurations.AsNoTracking()
            .Where(c => c.WebsiteId == websiteId && c.IsDraft)
            .Select(c => (int?)c.ConfigurationVersion).FirstOrDefaultAsync(ct);

        var publishedVersion = await db.WebsiteConfigurations.AsNoTracking()
            .Where(c => c.WebsiteId == websiteId && !c.IsDraft)
            .Select(c => (int?)c.ConfigurationVersion).MaxAsync(ct);

        return new WebsiteDto(
            website.Id, website.TenantId, website.RestaurantId, website.Restaurant.Name,
            website.WebsiteTemplateId, website.Template.Name, website.Template.TemplateKey,
            website.Name, website.Slug, website.Status.ToString(), website.IsPublished,
            website.PublishedAt, draftVersion, publishedVersion, website.CreatedAt, website.UpdatedAt);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_Websites_TenantId_Slug", StringComparison.Ordinal) == true ||
        ex.InnerException?.Message.Contains("Violation of UNIQUE KEY", StringComparison.OrdinalIgnoreCase) == true;
}
