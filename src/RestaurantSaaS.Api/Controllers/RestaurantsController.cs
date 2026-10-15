using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurants")]
public sealed class RestaurantsController(IRestaurantService restaurants) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantCreate)]
    public async Task<ActionResult<ApiResponse<RestaurantDto>>> Create(
        CreateRestaurantRequest request,
        CancellationToken ct)
    {
        var result = await restaurants.CreateAsync(request, ct);
        return Ok(ApiResponse<RestaurantDto>.Ok(result));
    }

    [HttpGet]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RestaurantDto>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await restaurants.ListAsync(page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<RestaurantDto>>.Ok(result));
    }

    [HttpGet("{restaurantId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantView)]
    public async Task<ActionResult<ApiResponse<RestaurantDto>>> GetById(
        Guid restaurantId,
        CancellationToken ct)
    {
        var result = await restaurants.GetByIdAsync(restaurantId, ct);
        return Ok(ApiResponse<RestaurantDto>.Ok(result));
    }

    [HttpPut("{restaurantId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantUpdate)]
    public async Task<ActionResult<ApiResponse<RestaurantDto>>> Update(
        Guid restaurantId,
        UpdateRestaurantRequest request,
        CancellationToken ct)
    {
        var result = await restaurants.UpdateAsync(restaurantId, request, ct);
        return Ok(ApiResponse<RestaurantDto>.Ok(result));
    }

    [HttpPatch("{restaurantId:guid}/status")]
    public async Task<ActionResult<ApiResponse<RestaurantDto>>> ChangeStatus(
        Guid restaurantId,
        UpdateRestaurantStatusRequest request,
        CancellationToken ct)
    {
        var result = await restaurants.ChangeStatusAsync(restaurantId, request.Status, ct);
        return Ok(ApiResponse<RestaurantDto>.Ok(result));
    }

    [HttpDelete("{restaurantId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.RestaurantUpdate)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(
        Guid restaurantId,
        CancellationToken ct)
    {
        await restaurants.SoftDeleteAsync(restaurantId, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
