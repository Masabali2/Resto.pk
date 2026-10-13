using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Websites;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Platform-administration of the GLOBAL template catalogue. Mutating any of
/// these endpoints requires the platform-level PlatformAdmin role (AspNetUserRoles),
/// never a tenant staff role — templates are shared platform resources.
/// </summary>
[ApiController]
[Route("api/v1/website-templates")]
[Authorize(Policy = IdentityPolicyNames.PlatformAdminOnly)]
public sealed class WebsiteTemplatesController(IWebsiteTemplateService templates) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WebsiteTemplateDto>>>> List(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<WebsiteTemplateDto>>.Ok(await templates.ListAsync(includeInactive, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WebsiteTemplateDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WebsiteTemplateDto>.Ok(await templates.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebsiteTemplateDto>>> Create([FromBody] CreateWebsiteTemplateRequest request, CancellationToken ct)
    {
        var dto = await templates.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<WebsiteTemplateDto>.Ok(dto));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WebsiteTemplateDto>>> Update(Guid id, [FromBody] UpdateWebsiteTemplateRequest request, CancellationToken ct)
        => Ok(ApiResponse<WebsiteTemplateDto>.Ok(await templates.UpdateAsync(id, request, ct)));
}
