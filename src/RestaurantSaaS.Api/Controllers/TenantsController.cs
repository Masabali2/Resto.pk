using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;
using RestaurantSaaS.Application.Features.Tenants;
using RestaurantSaaS.Application.Features.Tenants.DTOs;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public sealed class TenantsController(
    ITenantService tenants,
    ITenantMembershipService memberships,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TenantAdminDto>>> Create(
        CreateTenantRequest request,
        CancellationToken ct)
    {
        var result = await tenants.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { tenantId = result.Id }, ApiResponse<TenantAdminDto>.Ok(result));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantSummaryDto>>>> GetMine(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<TenantSummaryDto>>.Fail(
                "AUTH_UNAUTHORIZED", "Authentication is required for this operation."));
        }

        var rows = await memberships.GetMembershipsForUserAsync(userId.Value, ct);
        var dtos = rows.Select(m => m.ToDto()).ToList();

        return Ok(ApiResponse<IReadOnlyList<TenantSummaryDto>>.Ok(dtos));
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetById(Guid tenantId, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized(ApiResponse<TenantDto>.Fail(
                "AUTH_UNAUTHORIZED", "Authentication is required for this operation."));
        }

        var membership = await memberships.GetMembershipAsync(userId.Value, tenantId, ct);
        if (membership is null || membership.Status != (int)Domain.Enums.MembershipStatus.Active)
        {
            return Forbid();
        }

        Request.Headers.Append(AuthenticatedTenantResolver.TenantHeaderName, tenantId.ToString());
        var result = await tenants.GetCurrentAsync(ct);

        return Ok(ApiResponse<TenantDto>.Ok(result));
    }

    [HttpPut("{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Update(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized(ApiResponse<TenantDto>.Fail(
                "AUTH_UNAUTHORIZED", "Authentication is required for this operation."));
        }

        var membership = await memberships.GetMembershipAsync(userId.Value, tenantId, ct);
        if (membership is null || membership.Status != (int)Domain.Enums.MembershipStatus.Active)
        {
            return Forbid();
        }

        if (!Application.Common.Authorization.Permissions.RolePermissions
                .TryGetValue(membership.RoleName, out var permissions) ||
            !permissions.Contains(Application.Common.Authorization.Permissions.TenantUpdate))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TenantDto>.Fail(
                "FORBIDDEN", $"Your role '{membership.RoleName}' cannot perform this operation."));
        }

        Request.Headers.Append(AuthenticatedTenantResolver.TenantHeaderName, tenantId.ToString());
        var result = await tenants.UpdateCurrentAsync(request, ct);

        return Ok(ApiResponse<TenantDto>.Ok(result));
    }
}
