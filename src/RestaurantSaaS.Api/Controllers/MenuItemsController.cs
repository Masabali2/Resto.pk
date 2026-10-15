using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/menu-items")]
public sealed class MenuItemsController(IMenuItemService items) : ControllerBase
{
    [HttpGet("{itemId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemView)]
    public async Task<ActionResult<ApiResponse<MenuItemDto>>> Get(Guid itemId, CancellationToken ct)
    {
        var result = await items.GetByIdAsync(itemId, ct);
        return Ok(ApiResponse<MenuItemDto>.Ok(result));
    }

    [HttpPut("{itemId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemUpdate)]
    public async Task<ActionResult<ApiResponse<MenuItemDto>>> Update(Guid itemId, UpdateMenuItemRequest request, CancellationToken ct)
    {
        var result = await items.UpdateAsync(itemId, request, ct);
        return Ok(ApiResponse<MenuItemDto>.Ok(result));
    }

    [HttpPatch("{itemId:guid}/availability")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemUpdate)]
    public async Task<ActionResult<ApiResponse<MenuItemDto>>> SetAvailability(
        Guid itemId, UpdateMenuItemAvailabilityRequest request, CancellationToken ct)
    {
        var result = await items.SetAvailabilityAsync(itemId, request.IsAvailable, ct);
        return Ok(ApiResponse<MenuItemDto>.Ok(result));
    }

    [HttpPatch("{itemId:guid}/status")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemUpdate)]
    public async Task<ActionResult<ApiResponse<MenuItemDto>>> SetStatus(
        Guid itemId, UpdateMenuItemStatusRequest request, CancellationToken ct)
    {
        var result = await items.SetActiveAsync(itemId, request.IsActive, ct);
        return Ok(ApiResponse<MenuItemDto>.Ok(result));
    }

    [HttpDelete("{itemId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemDelete)]
    public async Task<IActionResult> Delete(Guid itemId, CancellationToken ct)
    {
        await items.SoftDeleteAsync(itemId, ct);
        return NoContent();
    }
}
