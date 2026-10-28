using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// ANONYMOUS read-only catalogue of ACTIVE website templates.
///
/// [AllowAnonymous] is deliberate: the public marketing site must show the
/// available templates before a visitor has an account or a tenant. The
/// endpoint is strictly read-only and returns a projection that excludes
/// administrative metadata and storage references. Administrative template
/// management stays on <see cref="WebsiteTemplatesController"/>
/// (PlatformAdminOnly) and tenant discovery stays on
/// <see cref="WebsitesController"/> (TenantMember + Website.View); neither
/// policy is relaxed here.
/// </summary>
[ApiController]
[Route("api/v1/public/website-templates")]
[AllowAnonymous]
public sealed class PublicWebsiteTemplatesController(IWebsiteTemplateService templates)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicWebsiteTemplateDto>>>> List(
        CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PublicWebsiteTemplateDto>>.Ok(
            await templates.ListPublicAsync(ct)));
}
