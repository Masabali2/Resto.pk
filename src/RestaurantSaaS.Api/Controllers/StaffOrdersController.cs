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
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await orderService.ListAsync(restaurantId, status, search, page, pageSize, ct);
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
    [HttpGet("summary")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task<ActionResult<ApiResponse<StaffOrderSummaryDto>>> GetSummary(
    Guid restaurantId,
    [FromQuery] string? startDate,
    [FromQuery] string? endDate,
    CancellationToken ct = default)
    {
        var summary = await orderService.GetSummaryAsync(
            restaurantId,
            ParseDate(startDate),
            ParseDate(endDate),
            ct);

        return Ok(ApiResponse<StaffOrderSummaryDto>.Ok(summary));
    }

    /// <summary>
    /// Daily order trend (revenue and order volume) for the restaurant within
    /// an optional date range. Read-only dashboard analytics.
    /// </summary>
    [HttpGet("analytics")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task<ActionResult<ApiResponse<OrderAnalyticsDto>>> GetAnalytics(
    Guid restaurantId,
    [FromQuery] string? startDate,
    [FromQuery] string? endDate,
    CancellationToken ct = default)
    {
        var analytics = await orderService.GetOrderAnalyticsAsync(
            restaurantId,
            ParseDate(startDate),
            ParseDate(endDate),
            ct);

        return Ok(ApiResponse<OrderAnalyticsDto>.Ok(analytics));
    }

    [HttpGet("create")]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task<ActionResult<ApiResponse<StaffCreateOrderDto>>> GetCreateContext(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var context = await orderService.GetCreateContextAsync(restaurantId, ct);

        return Ok(ApiResponse<StaffCreateOrderDto>.Ok(context));
    }

    [HttpPost]
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderCreate)]
    public async Task<ActionResult<ApiResponse<StaffOrderDto>>> Create(
        Guid restaurantId,
        [FromBody] RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest request,
        CancellationToken ct = default)
    {
        var order = await orderService.CreateAsync(restaurantId, request, ct);

        return Ok(ApiResponse<StaffOrderDto>.Ok(order));
    }

    /// <summary>
    /// Parses an optional "YYYY-MM-DD" query value into a server-side date.
    /// Returns null when the value is absent or not a valid calendar date.
    /// </summary>
    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        return DateOnly.TryParse(value, out var date) ? date : null;
    }
}
