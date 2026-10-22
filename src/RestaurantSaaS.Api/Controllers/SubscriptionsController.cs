using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Application.Features.Subscriptions.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Subscription endpoints for the ACTIVE workspace.
///
/// Every route derives the workspace from the server-resolved tenant context;
/// no route accepts a client supplied tenant id as proof of authorization
/// (the two `tenants/{tenantId}` routes exist for history and are re-verified
/// against the caller's membership and the active workspace).
/// </summary>
[ApiController]
[Authorize]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ITenantSubscriptionService subscriptions) : ControllerBase
{
    /// <summary>Plans that are currently offered to workspaces.</summary>
    [HttpGet("plans")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>> ListPlans(
        CancellationToken ct)
    {
        var result = await subscriptions.ListAvailablePlansAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<SubscriptionPlanDto>>.Ok(result));
    }

    /// <summary>
    /// Complete subscription state of the active workspace: workspace status,
    /// current subscription, trial eligibility and the available plans.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CurrentSubscriptionStateDto>>> GetMyState(CancellationToken ct)
    {
        var result = await subscriptions.GetMyStateAsync(ct);
        return Ok(ApiResponse<CurrentSubscriptionStateDto>.Ok(result));
    }

    /// <summary>Whether the active workspace may still consume its free trial.</summary>
    [HttpGet("me/trial-eligibility")]
    public async Task<ActionResult<ApiResponse<TrialEligibilityDto>>> GetTrialEligibility(CancellationToken ct)
    {
        var result = await subscriptions.GetTrialEligibilityAsync(ct);
        return Ok(ApiResponse<TrialEligibilityDto>.Ok(result));
    }

    /// <summary>
    /// Starts the 30-day free trial of the active workspace and activates the
    /// workspace in the same transaction. Only the workspace owner may call it.
    /// </summary>
    [HttpPost("me/trial")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> StartTrial(CancellationToken ct)
    {
        var result = await subscriptions.StartTrialAsync(ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(result));
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
