using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;
using RestaurantSaaS.Application.Features.Tenants;

namespace RestaurantSaaS.Api.Controllers.Platform;

[ApiController]
[Authorize(Policy = IdentityPolicyNames.PlatformAdminOnly)]
[Route("api/platform/subscriptions")]
public sealed class PlatformSubscriptionsController(ISubscriptionPlanService plans) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<PlatformSubscriptionOverviewDto>>> GetOverview(CancellationToken ct)
    {
        var result = await plans.GetPlatformOverviewAsync(ct);
        return Ok(ApiResponse<PlatformSubscriptionOverviewDto>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SubscriptionDto>>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? planId,
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PlatformSubscriptionQuery(status, planId, tenantId, page, pageSize);
        var result = await plans.ListForPlatformAsync(query, ct);
        return Ok(ApiResponse<PagedResult<SubscriptionDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await plans.GetByIdForPlatformAsync(id, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }
}

[ApiController]
[Authorize(Policy = IdentityPolicyNames.PlatformAdminOnly)]
[Route("api/platform/tenants")]
public sealed class PlatformTenantsController(ITenantService tenants) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantAdminDto>>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await tenants.ListForPlatformAsync(status, search, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<TenantAdminDto>>.Ok(result));
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantAdminDto>>> GetById(Guid tenantId, CancellationToken ct)
    {
        var result = await tenants.GetByIdForPlatformAsync(tenantId, ct);
        return Ok(ApiResponse<TenantAdminDto>.Ok(result));
    }

    [HttpPatch("{tenantId:guid}/status")]
    public async Task<ActionResult<ApiResponse<TenantAdminDto>>> ChangeStatus(
        Guid tenantId,
        UpdateTenantStatusRequest request,
        CancellationToken ct)
    {
        var result = await tenants.ChangeStatusForPlatformAsync(tenantId, request.Status, ct);
        return Ok(ApiResponse<TenantAdminDto>.Ok(result));
    }
}
