using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Platform-admin template catalogue management. Mutations are reachable only
/// through the PlatformAdminOnly policy at the API boundary; reads of ACTIVE
/// templates are exposed to tenants via IWebsiteService.ListAvailableTemplatesAsync
/// and anonymously via the public marketing endpoint (ListPublicAsync).
/// TemplateKey uniqueness is enforced at the database level (UNIQUE index) —
/// application pre-check exists only for a friendly error message.
/// </summary>
public sealed class WebsiteTemplateService(ApplicationDbContext db) : IWebsiteTemplateService
{
    public async Task<IReadOnlyList<WebsiteTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = db.WebsiteTemplates.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);

        var items = await query.OrderBy(t => t.Name).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    /// <summary>
    /// Reuses <see cref="ListAsync"/> so the public catalogue follows exactly
    /// the same active-only filter and Name ordering as the tenant catalogue.
    /// Only public display fields are projected; administrative metadata and
    /// the preview image storage reference never leave the API.
    /// </summary>
    public async Task<IReadOnlyList<PublicWebsiteTemplateDto>> ListPublicAsync(CancellationToken ct = default)
    {
        var templates = await ListAsync(includeInactive: false, ct);

        return templates
            .Select(t => new PublicWebsiteTemplateDto(
                t.Id,
                t.Name,
                t.Description,
                t.TemplateKey,
                t.DesignConfigurationJson))
            .ToList();
    }

    public async Task<WebsiteTemplateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await db.WebsiteTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("WebsiteTemplate", id);
        return ToDto(template);
    }

    public async Task<WebsiteTemplateDto> CreateAsync(CreateWebsiteTemplateRequest request, CancellationToken ct = default)
    {
        var key = request.TemplateKey.Trim().ToLowerInvariant();

        if (await db.WebsiteTemplates.AnyAsync(t => t.TemplateKey == key, ct))
            throw new ConflictException($"A template with key '{key}' already exists.");

        // Preview image must exist if referenced (platform-owned resource, no tenant scope).
        if (request.PreviewImageFileId is { } fileId &&
            !await db.Files.AnyAsync(f => f.Id == fileId && f.DeletedAt == null, ct))
            throw new NotFoundException("File", fileId);

        // A catalogue template is rendered by the generic template renderer,
        // so it is always created with a default design configuration.
        var designConfiguration = request.DesignConfigurationJson
            ?? throw new DomainException(
                "TEMPLATE_DESIGN_CONFIGURATION_REQUIRED",
                "A default design configuration is required to create a template.");

        var template = WebsiteTemplate.Create(request.Name, key, request.Description);
        template.AssignPreviewImage(request.PreviewImageFileId);
        template.SetDesignConfiguration(designConfiguration);

        db.WebsiteTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return ToDto(template);
    }

    public async Task<WebsiteTemplateDto> UpdateAsync(Guid id, UpdateWebsiteTemplateRequest request, CancellationToken ct = default)
    {
        var template = await db.WebsiteTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("WebsiteTemplate", id);

        template.Update(request.Name, request.Description, request.IsActive);

        if (request.DesignConfigurationJson is not null)
            template.SetDesignConfiguration(request.DesignConfigurationJson);

        await db.SaveChangesAsync(ct);

        return ToDto(template);
    }

    private static WebsiteTemplateDto ToDto(WebsiteTemplate t) =>
        new(
            t.Id,
            t.Name,
            t.Description,
            t.TemplateKey,
            t.PreviewImageFileId,
            t.IsActive,
            t.CreatedAt,
            t.DesignConfigurationJson);
}
