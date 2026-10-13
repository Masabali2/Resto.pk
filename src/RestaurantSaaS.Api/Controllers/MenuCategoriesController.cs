using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.MenuCategories;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.MenuItems;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class MenuCategoriesController(
    IMenuCategoryService categories,
    IMenuItemService items) : ControllerBase
{
    [HttpGet("menu-categories/{categoryId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryView)]
    public async Task<ActionResult<ApiResponse<MenuCategoryDto>>> Get(Guid categoryId, CancellationToken ct)
    {
        var result = await categories.GetByIdAsync(categoryId, ct);
        return Ok(ApiResponse<MenuCategoryDto>.Ok(result));
    }

    [HttpPut("menu-categories/{categoryId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryUpdate)]
    public async Task<ActionResult<ApiResponse<MenuCategoryDto>>> Update(
        Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken ct)
    {
        var result = await categories.UpdateAsync(categoryId, request, ct);
        return Ok(ApiResponse<MenuCategoryDto>.Ok(result));
    }

    [HttpPatch("menu-categories/{categoryId:guid}/status")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryUpdate)]
    public async Task<ActionResult<ApiResponse<MenuCategoryDto>>> SetStatus(
        Guid categoryId, UpdateMenuCategoryStatusRequest request, CancellationToken ct)
    {
        var result = await categories.SetActiveAsync(categoryId, request.IsActive, ct);
        return Ok(ApiResponse<MenuCategoryDto>.Ok(result));
    }

    [HttpDelete("menu-categories/{categoryId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryDelete)]
    public async Task<IActionResult> Delete(Guid categoryId, CancellationToken ct)
    {
        await categories.DeleteAsync(categoryId, ct);
        return NoContent();
    }

    [HttpPost("menu-categories/{categoryId:guid}/items")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemCreate)]
    public async Task<ActionResult<ApiResponse<MenuItemDto>>> CreateItem(
        Guid categoryId, CreateMenuItemRequest request, CancellationToken ct)
    {
        var result = await items.CreateAsync(categoryId, request, ct);
        return Ok(ApiResponse<MenuItemDto>.Ok(result));
    }

    [HttpGet("menu-categories/{categoryId:guid}/items")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MenuItemDto>>>> ListItems(
        Guid categoryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await items.ListByCategoryAsync(categoryId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<MenuItemDto>>.Ok(result));
    }
}
