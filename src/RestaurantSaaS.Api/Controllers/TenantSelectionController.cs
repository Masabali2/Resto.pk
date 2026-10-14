using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public sealed class TenantSelectionController(ITenantMembershipService memberships) : ControllerBase
{
    [HttpPost("{tenantId:guid}/switch")]
    public async Task<ActionResult<ApiResponse<TenantSummaryDto>>> Switch(
        Guid tenantId,
        CancellationToken ct)
    {
        var membership = await memberships.GetMembershipAsync(UserId(), tenantId, ct);

        if (membership is null || membership.Status != (int)Domain.Enums.MembershipStatus.Active)
        {
            return NotFound(ApiResponse<TenantSummaryDto>.Fail(
                "TENANT_MEMBERSHIP_NOT_FOUND",
                "You do not have an active membership in the requested tenant."));
        }

        Response.Headers.Append(AuthenticatedTenantResolver.TenantHeaderName, tenantId.ToString());

        return Ok(ApiResponse<TenantSummaryDto>.Ok(membership.ToDto()));
    }

    private Guid UserId() =>
        Guid.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value,
            out var id)
            ? id
            : throw new Application.Common.Exceptions.NotAuthenticatedException();
}
