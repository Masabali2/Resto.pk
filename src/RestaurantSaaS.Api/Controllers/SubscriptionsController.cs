using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ITenantSubscriptionService subscriptions) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CurrentSubscriptionStateDto>>> GetMyState(CancellationToken ct)
    {
        var result = await subscriptions.GetMyStateAsync(ct);
        return Ok(ApiResponse<CurrentSubscriptionStateDto>.Ok(result));
    }

    [HttpGet("tenants/{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetByTenant(
        Guid tenantId,
        CancellationToken ct)
    {
        var result = await subscriptions.GetByTenantAsync(tenantId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpGet("tenants/{tenantId:guid}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionDto>>>> ListForTenant(
        Guid tenantId,
        CancellationToken ct)
    {
        var result = await subscriptions.ListForTenantAsync(tenantId, ct);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionDto>>.Ok(result));
    }

    [HttpPost("me/start")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Start(
        StartTenantSubscriptionRequest request,
        CancellationToken ct)
    {
        var result = await subscriptions.StartAsync(request, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpPost("{subscriptionId:guid}/change-plan")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> ChangePlan(
        Guid subscriptionId,
        ChangeTenantSubscriptionPlanRequest request,
        CancellationToken ct)
    {
        var result = await subscriptions.ChangePlanAsync(subscriptionId, request, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpPost("{subscriptionId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> Cancel(
        Guid subscriptionId,
        CancellationToken ct)
    {
        var result = await subscriptions.CancelAsync(subscriptionId, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpPatch("{subscriptionId:guid}/status")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> SetStatus(
        Guid subscriptionId,
        SetSubscriptionStatusRequest request,
        CancellationToken ct)
    {
        var result = await subscriptions.SetStatusAsync(subscriptionId, request, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }

    [HttpPut("{subscriptionId:guid}/auto-renew")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> SetAutoRenew(
        Guid subscriptionId,
        SetAutoRenewRequest request,
        CancellationToken ct)
    {
        var result = await subscriptions.SetAutoRenewAsync(subscriptionId, request, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
    }
}
