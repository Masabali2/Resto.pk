using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.PublicCarts;
using RestaurantSaaS.Application.Features.PublicCarts.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Anonymous public cart operations for customer ordering.
///
/// Cart identity is the resolved restaurant + shopper session hash.
/// No staff Identity/JWT is required. Mutations are protected by antiforgery.
/// </summary>
[ApiController]
[AllowAnonymous]
[AutoValidateAntiforgeryToken]
[Route("api/v1/public/restaurants/{slug}/cart")]
public sealed class PublicCartController(
    IPublicRestaurantService restaurants,
    IPublicCartService cartService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PublicCartDto>>> Get(
        string slug,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<PublicCartDto>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        var cart = await cartService.GetCurrentCartAsync(restaurant.Id, ct);
        return Ok(ApiResponse<PublicCartDto>.Ok(cart));
    }

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<PublicCartItemDto>>> AddItem(
        string slug,
        [FromBody] AddPublicCartItemRequest request,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<PublicCartItemDto>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        var item = await cartService.AddItemAsync(restaurant.Id, request, ct);
        return Ok(ApiResponse<PublicCartItemDto>.Ok(item));
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<PublicCartItemDto>>> UpdateItem(
        string slug,
        Guid itemId,
        [FromBody] UpdatePublicCartItemRequest request,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<PublicCartItemDto>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        var item = await cartService.UpdateItemAsync(restaurant.Id, itemId, request, ct);
        return Ok(ApiResponse<PublicCartItemDto>.Ok(item));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveItem(
        string slug,
        Guid itemId,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<object>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        await cartService.RemoveItemAsync(restaurant.Id, itemId, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete]
    public async Task<ActionResult<ApiResponse<object>>> Clear(
        string slug,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<object>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        await cartService.ClearAsync(restaurant.Id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private async Task<PublicOrderingRestaurantDto?> ResolveRestaurantAsync(
        string slug,
        CancellationToken ct)
    {
        return await restaurants.GetValidRestaurantForOrderingAsync(slug, ct);
    }
}
