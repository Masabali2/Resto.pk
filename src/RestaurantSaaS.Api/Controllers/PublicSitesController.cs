using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Websites;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// ANONYMOUS public read endpoints for published restaurant websites.
/// [AllowAnonymous] is deliberate: customers have no accounts in V1. The only
/// input is a slug, resolved server-side through publication state — there is
/// no path by which an anonymous caller can reach drafts, other tenants' data,
/// or management functionality. Unknown/unpublished slugs both return 404.
/// </summary>
[ApiController]
[Route("api/v1/public/sites")]
[AllowAnonymous]
public sealed class PublicSitesController(IPublicSiteService sites) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<ActionResult<ApiResponse<PublicSiteDto>>> GetBySlug(string slug, CancellationToken ct)
        => Ok(ApiResponse<PublicSiteDto>.Ok(await sites.GetBySlugAsync(slug, ct)));

    [HttpGet("{slug}/menu")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicMenuDto>>>> GetMenu(string slug, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PublicMenuDto>>.Ok(await sites.GetMenuBySlugAsync(slug, ct)));
}
