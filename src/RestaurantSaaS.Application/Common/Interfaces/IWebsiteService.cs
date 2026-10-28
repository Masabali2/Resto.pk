using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Platform-administration of the global website template catalogue.
/// Templates are NOT tenant-owned; only PlatformAdmin may mutate them
/// (policy: IdentityPolicyNames.PlatformAdminOnly). Tenant staff use
/// IWebsiteService.ListAvailableTemplatesAsync to browse active templates.
/// </summary>
public interface IWebsiteTemplateService
{
    Task<IReadOnlyList<WebsiteTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<WebsiteTemplateDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WebsiteTemplateDto> CreateAsync(CreateWebsiteTemplateRequest request, CancellationToken ct = default);
    Task<WebsiteTemplateDto> UpdateAsync(Guid id, UpdateWebsiteTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Anonymous read model of the ACTIVE template catalogue. Shares the
    /// filtering and ordering of <see cref="ListAsync"/> but projects only
    /// public fields. Templates are platform-owned, so no tenant context is
    /// required or consulted.
    /// </summary>
    Task<IReadOnlyList<PublicWebsiteTemplateDto>> ListPublicAsync(CancellationToken ct = default);
}

/// <summary>
/// Tenant-scoped website management: create websites for the tenant's own
/// restaurants, edit drafts, publish/unpublish. TenantId is NEVER accepted
/// from requests — it comes exclusively from ITenantContext (server-resolved).
/// Draft/published configuration isolation is enforced here and in the domain.
/// </summary>
public interface IWebsiteService
{
    Task<IReadOnlyList<WebsiteTemplateDto>> ListAvailableTemplatesAsync(CancellationToken ct = default);

    Task<WebsiteDto> CreateAsync(CreateWebsiteRequest request, CancellationToken ct = default);
    Task<PagedResult<WebsiteDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<WebsiteDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WebsiteDto> UpdateAsync(Guid id, UpdateWebsiteRequest request, CancellationToken ct = default);

    /// <summary>Saves/replaces the draft version. Anonymous visitors keep seeing the current published version.</summary>
    Task<WebsiteDto> SaveDraftAsync(Guid id, SaveWebsiteDraftRequest request, CancellationToken ct = default);

    Task<WebsiteDto> PublishAsync(Guid id, CancellationToken ct = default);
    Task<WebsiteDto> UnpublishAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Anonymous public read model. Resolves a website by its slug ONLY through
/// published + active state; never accepts TenantId/RestaurantId from callers.
/// Returns 404-equivalent (NotFoundException) for unknown/unpublished slugs so
/// existence of other tenants' sites is not leaked.
/// </summary>
public interface IPublicSiteService
{
    Task<PublicSiteDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<PublicMenuDto>> GetMenuBySlugAsync(string slug, CancellationToken ct = default);
}
