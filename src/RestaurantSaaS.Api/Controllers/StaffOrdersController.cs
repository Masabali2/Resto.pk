using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Api.Controllers;

/// <summary>
/// Authenticated staff order management.
///
/// All endpoints require JWT authentication and tenant-scoped authorization.
/// Staff can only access orders belonging to their tenant's restaurants.
/// The shopper session is never used as an authorization mechanism.
/// </summary>
[ApiController]
[Authorize]
[Route("api/restaurants/{restaurantId:guid}/orders")]
public sealed class StaffOrdersController(
    IStaffOrderService orderService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task<ActionResult<ApiResponse<PagedResult<StaffOrderDto>>>> List(
        Guid restaurantId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await orderService.ListAsync(restaurantId, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<StaffOrderDto>>.Ok(result));
    }

    [HttpGet("{orderId:guid}")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task<ActionResult<ApiResponse<StaffOrderDto>>> GetById(
        Guid restaurantId,
        Guid orderId,
        CancellationToken ct = default)
    {
        var order = await orderService.GetByIdAsync(restaurantId, orderId, ct);
        if (order is null)
            return NotFound(ApiResponse<StaffOrderDto>.Fail("NOT_FOUND", "Order not found."));

        return Ok(ApiResponse<StaffOrderDto>.Ok(order));
    }

    [HttpPatch("{orderId:guid}/status")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderUpdate)]
    public async Task<ActionResult<ApiResponse<StaffOrderDto>>> TransitionStatus(
        Guid restaurantId,
        Guid orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException("STAFF_AUTH_REQUIRED", "Staff authentication is required.");

        var order = await orderService.TransitionStatusAsync(restaurantId, orderId, request, userId, ct);
        return Ok(ApiResponse<StaffOrderDto>.Ok(order));
    }
}
