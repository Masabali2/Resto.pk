using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.PublicOrders.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Anonymous public order creation and tracking from the shopper cart.
///
/// No staff Identity/JWT is required. Mutations are protected by antiforgery.
/// The raw tracking token is returned exactly once during order creation.
/// Tracking lookup compares the SHA-256 hash of the raw token against the
/// persisted TrackingTokenHash; the raw token is never stored or returned.
/// </summary>
[ApiController]
[AllowAnonymous]
[AutoValidateAntiforgeryToken]
[Route("api/v1/public/restaurants/{slug}/orders")]
public sealed class PublicOrdersController(
    IPublicRestaurantService restaurants,
    IPublicOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreatePublicOrderResponse>>> Create(
        string slug,
        [FromBody] CreatePublicOrderRequest request,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<CreatePublicOrderResponse>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        var response = await orderService.CreateOrderFromCartAsync(restaurant.Id, request, ct);
        return Ok(ApiResponse<CreatePublicOrderResponse>.Ok(response));
    }

    /// <summary>
    /// Public order tracking. Resolves an order by the SHA-256 hash of the
    /// raw tracking token. Safe (GET) — no antiforgery required.
    /// </summary>
    [HttpGet("track/{trackingToken}")]
    public async Task<ActionResult<ApiResponse<PublicOrderTrackingDto>>> Track(
        string slug,
        string trackingToken,
        CancellationToken ct)
    {
        var restaurant = await ResolveRestaurantAsync(slug, ct);
        if (restaurant is null)
            return NotFound(ApiResponse<PublicOrderTrackingDto>.Fail(
                "NOT_FOUND", "Restaurant not found or not available for ordering."));

        var order = await orderService.GetOrderByTrackingTokenAsync(restaurant.Id, trackingToken, ct);
        if (order is null)
            return NotFound(ApiResponse<PublicOrderTrackingDto>.Fail(
                "NOT_FOUND", "Order not found."));

        return Ok(ApiResponse<PublicOrderTrackingDto>.Ok(order));
    }

    private async Task<PublicOrderingRestaurantDto?> ResolveRestaurantAsync(
        string slug,
        CancellationToken ct)
    {
        return await restaurants.GetValidRestaurantForOrderingAsync(slug, ct);
    }
}
