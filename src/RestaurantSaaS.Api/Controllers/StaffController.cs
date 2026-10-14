using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Staff;
using RestaurantSaaS.Application.Features.Staff.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/staff")]
public sealed class StaffController(IStaffService staff) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffView)]
    public async Task<ActionResult<ApiResponse<PagedResult<StaffMemberDto>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await staff.ListAsync(page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<StaffMemberDto>>.Ok(result));
    }

    [HttpGet("{membershipId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffView)]
    public async Task<ActionResult<ApiResponse<StaffMemberDto>>> GetById(
        Guid membershipId,
        CancellationToken ct)
    {
        var result = await staff.GetByIdAsync(membershipId, ct);
        return Ok(ApiResponse<StaffMemberDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffInvite)]
    public async Task<ActionResult<ApiResponse<StaffMemberDto>>> Invite(
        InviteStaffRequest request,
        CancellationToken ct)
    {
        var result = await staff.InviteAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { membershipId = result.MembershipId }, ApiResponse<StaffMemberDto>.Ok(result));
    }

    [HttpPut("{membershipId:guid}/role")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffAssignRole)]
    public async Task<ActionResult<ApiResponse<StaffMemberDto>>> AssignRole(
        Guid membershipId,
        AssignStaffRoleRequest request,
        CancellationToken ct)
    {
        var result = await staff.AssignRoleAsync(membershipId, request.RoleName, ct);
        return Ok(ApiResponse<StaffMemberDto>.Ok(result));
    }

    [HttpPatch("{membershipId:guid}/status")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffUpdate)]
    public async Task<ActionResult<ApiResponse<StaffMemberDto>>> ChangeStatus(
        Guid membershipId,
        UpdateStaffStatusRequest request,
        CancellationToken ct)
    {
        var result = await staff.ChangeStatusAsync(membershipId, request.Status, ct);
        return Ok(ApiResponse<StaffMemberDto>.Ok(result));
    }

    [HttpDelete("{membershipId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.StaffRemove)]
    public async Task<ActionResult<ApiResponse<object?>>> Remove(
        Guid membershipId,
        CancellationToken ct)
    {
        await staff.RemoveAsync(membershipId, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
