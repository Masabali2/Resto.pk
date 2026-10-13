using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Websites;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Anonymous public read model for published restaurant websites.
///
/// SECURITY MODEL:
///  - No tenant context, no authentication required — but the ONLY entry point
///    is a validated slug; TenantId/RestaurantId are never accepted from callers.
///  - Queries filter on IsPublished + Status==Published + active restaurant/menu/
///    category/item state, so drafts and unpublished sites are unreachable even
///    if a slug is guessed.
///  - Unknown OR unpublished slugs produce the same NotFoundException (404) —
///    indistinguishable to anonymous callers, so no existence leakage across tenants.
///  - Projection (Select) keeps EF entities out of responses and limits payload.
///  - Public display rule (documented Phase-5 decision): unavailable items ARE
///    shown with IsAvailable=false so customers see full menus; inactive/draft
///    content is never returned.
/// </summary>
public sealed class PublicSiteService(ApplicationDbContext db) : IPublicSiteService
{
    public async Task<PublicSiteDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = Domain.Entities.Websites.Website.NormalizeSlug(slug);

        var site = await db.Websites.AsNoTracking()
            .Where(w => w.Slug == normalized && w.IsPublished && w.Status == WebsiteStatus.Published)
            .Select(w => new
            {
                w.Name,
                w.Slug,
                w.PublishedAt,
                Restaurant = new
                {
                    w.Restaurant.Name,
                    w.Restaurant.Description,
                    w.Restaurant.Phone,
                    w.Restaurant.Email,
                    w.Restaurant.City,
                    w.Restaurant.Country,
                    w.Restaurant.TimeZone,
                    w.Restaurant.IsDeleted,
                    w.Restaurant.Status
                },
                Template = new { w.Template.Name, w.Template.TemplateKey },
                PublishedConfig = w.Configurations
                    .Where(c => !c.IsDraft)
                    .OrderByDescending(c => c.ConfigurationVersion)
                    .Select(c => new { c.ConfigurationJson, c.ConfigurationVersion })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (site is null || site.Restaurant.IsDeleted ||
            site.Restaurant.Status == RestaurantStatus.Closed)
        {
            throw new NotFoundException("Website", normalized); // same shape as unknown slug
        }

        if (site.PublishedConfig is null)
        {
            // Defensive: a published website without a frozen configuration should not
            // exist; treat as not-found rather than serving partial content.
            throw new NotFoundException("Website", normalized);
        }

        return new PublicSiteDto(
            site.Name,
            site.Slug,
            new PublicRestaurantDto(
                site.Restaurant.Name, site.Restaurant.Description, site.Restaurant.Phone,
                site.Restaurant.Email, site.Restaurant.City, site.Restaurant.Country,
                site.Restaurant.TimeZone),
            new PublicTemplateDto(site.Template.Name, site.Template.TemplateKey),
            site.PublishedConfig.ConfigurationJson,
            site.PublishedConfig.ConfigurationVersion,
            site.PublishedAt ?? DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<PublicMenuDto>> GetMenuBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = Domain.Entities.Websites.Website.NormalizeSlug(slug);

        // Ownership chain resolved entirely server-side: slug -> published website
        // -> its restaurant -> that restaurant's ACTIVE menus only.
        var menus = await db.Menus.AsNoTracking()
            .Where(m => m.IsActive &&
                        db.Websites.Any(w => w.Slug == normalized && w.IsPublished &&
                                             w.Status == WebsiteStatus.Published &&
                                             w.RestaurantId == m.RestaurantId &&
                                             w.TenantId == m.TenantId))
            .OrderBy(m => m.Name)
            .Select(m => new PublicMenuDto(
                m.Id,
                m.Name,
                m.Description,
                m.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new PublicMenuCategoryDto(
                        c.Id,
                        c.Name,
                        c.Description,
                        c.DisplayOrder,
                        c.Items
                            .Where(i => i.IsActive && !i.IsDeleted)
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PublicMenuItemDto(
                                i.Id, i.Name, i.Description, i.Price, i.Currency,
                                i.DisplayOrder, i.IsAvailable, i.ImageFileId))
                            .ToList()))
                    .ToList()))
            .ToListAsync(ct);

        if (menus.Count == 0)
        {
            // Distinguish "no such site" from "site without menus": re-check existence.
            var siteExists = await db.Websites.AsNoTracking()
                .AnyAsync(w => w.Slug == normalized && w.IsPublished && w.Status == WebsiteStatus.Published, ct);
            if (!siteExists)
                throw new NotFoundException("Website", normalized);
        }

        return menus;
    }
}
