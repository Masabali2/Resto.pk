//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using RestaurantSaaS.Application.Common.Authorization;
//using RestaurantSaaS.Application.Common.Models;
//using RestaurantSaaS.Application.Common.Options;
//using RestaurantSaaS.Application.Features.MenuCategories;
//using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
//using RestaurantSaaS.Application.Features.MenuItems;
//using RestaurantSaaS.Application.Features.Menus;
//using RestaurantSaaS.Application.Features.Menus.DTOs;

//namespace RestaurantSaaS.Api.Controllers;

//[ApiController]
//[Authorize]
//[Route("api/v1")]
//public sealed class MenusController(
//    IMenuService menus,
//    IMenuCategoryService categories,
//    IMenuItemService items) : ControllerBase
//{
//    [HttpPost("restaurants/{restaurantId:guid}/menus")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCreate)]
//    public async Task<ActionResult<ApiResponse<MenuDto>>> Create(Guid restaurantId, CreateMenuRequest request, CancellationToken ct)
//    {
//        var result = await menus.CreateAsync(restaurantId, request, ct);
//        return Ok(ApiResponse<MenuDto>.Ok(result));
//    }

//    [HttpGet("restaurants/{restaurantId:guid}/menus")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuView)]
//    public async Task<ActionResult<ApiResponse<PagedResult<MenuDto>>>> List(
//        Guid restaurantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
//    {
//        var result = await menus.ListByRestaurantAsync(restaurantId, page, pageSize, ct);
//        return Ok(ApiResponse<PagedResult<MenuDto>>.Ok(result));
//    }

//    [HttpGet("menus/{menuId:guid}")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuView)]
//    public async Task<ActionResult<ApiResponse<MenuDetailsDto>>> Get(Guid menuId, CancellationToken ct)
//    {
//        var result = await menus.GetByIdAsync(menuId, ct);
//        return Ok(ApiResponse<MenuDetailsDto>.Ok(result));
//    }

//    [HttpPut("menus/{menuId:guid}")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuUpdate)]
//    public async Task<ActionResult<ApiResponse<MenuDto>>> Update(Guid menuId, UpdateMenuRequest request, CancellationToken ct)
//    {
//        var result = await menus.UpdateAsync(menuId, request, ct);
//        return Ok(ApiResponse<MenuDto>.Ok(result));
//    }

//    [HttpPatch("menus/{menuId:guid}/status")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuUpdate)]
//    public async Task<ActionResult<ApiResponse<MenuDto>>> SetStatus(Guid menuId, UpdateMenuStatusRequest request, CancellationToken ct)
//    {
//        var result = await menus.SetActiveAsync(menuId, request.IsActive, ct);
//        return Ok(ApiResponse<MenuDto>.Ok(result));
//    }

//    [HttpDelete("menus/{menuId:guid}")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuDelete)]
//    public async Task<ActionResult<ApiResponse<object?>>> Archive(Guid menuId, CancellationToken ct)
//    {
//        await menus.ArchiveAsync(menuId, ct);
//        return Ok(ApiResponse<object?>.Ok(null));
//    }

//    [HttpPost("menus/{menuId:guid}/categories")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryCreate)]
//    public async Task<ActionResult<ApiResponse<MenuCategoryDto>>> CreateCategory(
//        Guid menuId, CreateMenuCategoryRequest request, CancellationToken ct)
//    {
//        var result = await categories.CreateAsync(menuId, request, ct);
//        return Ok(ApiResponse<MenuCategoryDto>.Ok(result));
//    }

//    [HttpGet("menus/{menuId:guid}/categories")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryView)]
//    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuCategoryDto>>>> ListCategories(Guid menuId, CancellationToken ct)
//    {
//        var result = await categories.ListByMenuAsync(menuId, ct);
//        return Ok(ApiResponse<IReadOnlyList<MenuCategoryDto>>.Ok(result));
//    }

//    [HttpPatch("menus/{menuId:guid}/categories/order")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuCategoryUpdate)]
//    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuCategoryDto>>>> ReorderCategories(
//        Guid menuId, ReorderMenuCategoriesRequest request, CancellationToken ct)
//    {
//        var result = await categories.ReorderAsync(menuId, request, ct);
//        return Ok(ApiResponse<IReadOnlyList<MenuCategoryDto>>.Ok(result));
//    }

//    [HttpGet("menus/{menuId:guid}/items")]
//    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.MenuItemView)]
//    public async Task<ActionResult<ApiResponse<PagedResult<MenuItems.DTOs.MenuItemDto>>>> ListItems(
//        Guid menuId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
//    {
//        var result = await items.ListByMenuAsync(menuId, page, pageSize, ct);
//        return Ok(ApiResponse<PagedResult<MenuItems.DTOs.MenuItemDto>>.Ok(result));
//    }
//}
