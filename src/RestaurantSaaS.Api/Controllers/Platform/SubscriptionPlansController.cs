using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;

namespace RestaurantSaaS.Api.Controllers.Platform;

[ApiController]
[Authorize(Policy = IdentityPolicyNames.PlatformAdminOnly)]
[Route("api/platform/subscription-plans")]
public sealed class SubscriptionPlansController(ISubscriptionPlanService plans) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SubscriptionPlanDto>>>> List(
        [FromQuery] bool onlyActive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await plans.ListAsync(onlyActive, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<SubscriptionPlanDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await plans.GetByIdAsync(id, ct);
        return Ok(ApiResponse<SubscriptionPlanDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> Create(
        CreateSubscriptionPlanRequest request,
        CancellationToken ct)
    {
        var result = await plans.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<SubscriptionPlanDto>.Ok(result));
    }

    [HttpPut("{id:guid}/limits")]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> UpdateLimits(
        Guid id,
        UpdateSubscriptionPlanLimitsRequest request,
        CancellationToken ct)
    {
        var result = await plans.UpdateLimitsAsync(id, request, ct);
        return Ok(ApiResponse<SubscriptionPlanDto>.Ok(result));
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<ApiResponse<object?>>> SetActive(
        Guid id,
        [FromQuery] bool isActive,
        CancellationToken ct)
    {
        await plans.SetActiveAsync(id, isActive, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
