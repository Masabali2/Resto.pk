using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Tenant-scoped website management. No route or body ever carries TenantId —
/// the tenant comes from the server-resolved context (middleware + membership).
/// </summary>
[ApiController]
[Route("api/v1/websites")]
[Authorize(Policy = IdentityPolicyNames.TenantMember)]
public sealed class WebsitesController(IWebsiteService websites) : ControllerBase
{
    /// <summary>Active templates available for selection in the current tenant.</summary>
    [HttpGet("templates")]
    [Authorize(Policy = "Permission.Website.View")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WebsiteTemplateDto>>>> ListTemplates(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<WebsiteTemplateDto>>.Ok(await websites.ListAvailableTemplatesAsync(ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> Create([FromBody] CreateWebsiteRequest request, CancellationToken ct)
    {
        var dto = await websites.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, ApiResponse<WebsiteDto>.Ok(dto));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WebsiteDto>>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<WebsiteDto>>.Ok(await websites.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> GetById(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDto>.Ok(await websites.GetByIdAsync(id, ct)));

    /// <summary>Reads the tenant's saved DRAFT configuration for editing.</summary>
    [HttpGet("{id:guid}/draft")]
    public async Task<ActionResult<ApiResponse<WebsiteDraftDto>>> GetDraft(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDraftDto>.Ok(await websites.GetDraftAsync(id, ct)));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> Update(Guid id, [FromBody] UpdateWebsiteRequest request, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDto>.Ok(await websites.UpdateAsync(id, request, ct)));

    /// <summary>Saves/replaces the draft configuration. Public visitors keep seeing the published version.</summary>
    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> SaveDraft(Guid id, [FromBody] SaveWebsiteDraftRequest request, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDto>.Ok(await websites.SaveDraftAsync(id, request, ct)));

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> Publish(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDto>.Ok(await websites.PublishAsync(id, ct)));

    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> Unpublish(Guid id, CancellationToken ct)
        => Ok(ApiResponse<WebsiteDto>.Ok(await websites.UnpublishAsync(id, ct)));
}
