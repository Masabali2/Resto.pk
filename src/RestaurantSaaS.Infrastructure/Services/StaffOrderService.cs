using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Authenticated staff order management.
///
/// Tenant scope comes exclusively from ITenantContext (server-resolved from the
/// authenticated user's StaffMembership). Staff can only see and modify orders
/// belonging to their tenant's restaurants. The shopper session is never used
/// as an authorization mechanism for staff endpoints.
/// </summary>
public sealed class StaffOrderService(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IStaffOrderService
{
    public async Task<PagedResult<StaffOrderDto>> ListAsync(
        Guid? restaurantId,
        OrderStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.OrderView, ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantContext.GetRequiredTenantId());

        if (restaurantId.HasValue)
            query = query.Where(o => o.RestaurantId == restaurantId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .ToListAsync(ct);

        return new PagedResult<StaffOrderDto>
        {
            Items = rows.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<StaffOrderDto?> GetByIdAsync(
        Guid restaurantId,
        Guid orderId,
        CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.OrderView, ct);

        var order = await LoadOwnedOrderAsync(restaurantId, orderId, ct);
        if (order is null)
            return null;

        return MapToDto(order);
    }

    public async Task<StaffOrderDto> TransitionStatusAsync(
        Guid restaurantId,
        Guid orderId,
        UpdateOrderStatusRequest request,
        Guid changedByUserId,
        CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(
            Permissions.OrderUpdate,
            ct,
            request.NewStatus == OrderStatus.Cancelled ? Permissions.OrderCancel : null);

        var order = await LoadOwnedOrderAsync(restaurantId, orderId, ct)
            ?? throw new NotFoundException("Order", orderId);

        var utcNow = dateTimeProvider.UtcNow;

        var history = order.TransitionTo(
            request.NewStatus,
            changedByUserId,
            request.Notes,
            utcNow);

        // The history entity has a client-generated Guid key. When it is
        // discovered through the navigation of an already-tracked order,
        // EF Core attaches it as Modified (assuming it already exists) and
        // the subsequent UPDATE affects no rows. Track it explicitly as a
        // new row so the INSERT is issued.
        db.OrderStatusHistories.Add(history);

        await db.SaveChangesAsync(ct);

        return MapToDto(order);
    }

    // ------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Resolves + validates the acting tenant: context present, membership ACTIVE,
    /// role grants the required permission, and tenant operational.
    /// </summary>
    private async Task<Guid> GetActiveTenantIdAsync(
        string requiredPermission,
        CancellationToken ct,
        string? additionalPermission = null)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(requiredPermission) ||
            (additionalPermission is not null && !perms.Contains(additionalPermission)))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        var tenantOperational = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);
        if (!tenantOperational)
            throw new DomainException("TENANT_NOT_ACTIVE", "This tenant is not active.");

        return tenantId;
    }

    private async Task<Order?> LoadOwnedOrderAsync(Guid restaurantId, Guid orderId, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? null;

        if (order is null)
            return null;

        if (order.TenantId != tenantId)
            throw new CrossTenantAccessException();

        // An order from the same tenant but a different restaurant must be
        // indistinguishable from a missing order. Returning null here yields
        // 404 instead of 403, so the endpoint does not reveal that the order
        // exists under another restaurant.
        if (order.RestaurantId != restaurantId)
            return null;

        return order;
    }

    public async Task<StaffOrderSummaryDto> GetSummaryAsync(
        Guid restaurantId,
        CancellationToken ct = default)
    {
        var tenantId = await GetActiveTenantIdAsync(
            Permissions.OrderView,
            ct);

        // Explicit ownership check in addition to EF Core tenant filters.
        var restaurant = await db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == restaurantId && r.TenantId == tenantId,
                ct)
            ?? throw new NotFoundException("Restaurant", restaurantId);

        var orders = db.Orders
            .AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId &&
                o.RestaurantId == restaurantId);

        // Total orders across the restaurant's complete history.
        var totalOrders = await orders.CountAsync(ct);

        // Orders that are currently in the kitchen pipeline.
        var pendingKitchen = await orders.CountAsync(
            o => o.Status == OrderStatus.New ||
                 o.Status == OrderStatus.Confirmed ||
                 o.Status == OrderStatus.Preparing,
            ct);

        // Determine today's UTC boundaries using the restaurant's timezone.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            restaurant.TimeZone);

        var utcNow = DateTime.SpecifyKind(
            dateTimeProvider.UtcNow,
            DateTimeKind.Utc);

        var restaurantLocalDate =
            TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone).Date;

        var localDayStart = DateTime.SpecifyKind(
            restaurantLocalDate,
            DateTimeKind.Unspecified);

        var localDayEnd = localDayStart.AddDays(1);

        var utcDayStart = TimeZoneInfo.ConvertTimeToUtc(
            localDayStart,
            timeZone);

        var utcDayEnd = TimeZoneInfo.ConvertTimeToUtc(
            localDayEnd,
            timeZone);

        // CompletedToday is based on CompletedAt, not CreatedAt.
        var completedToday = await orders.CountAsync(
            o => o.Status == OrderStatus.Completed &&
                 o.CompletedAt.HasValue &&
                 o.CompletedAt.Value >= utcDayStart &&
                 o.CompletedAt.Value < utcDayEnd,
            ct);

        // Revenue follows the agreed business rule:
        // sum TotalAmount for completed orders, regardless of PaymentStatus.
        var totalRevenue = await orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        // The current Resto.pk ordering flow uses one currency per order.
        // Use the most recent order's currency; fall back to the platform
        // default when the restaurant has no orders yet.
        var currency = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.Currency)
            .FirstOrDefaultAsync(ct)
            ?? Money.DefaultCurrency;

        return new StaffOrderSummaryDto(
            totalOrders,
            pendingKitchen,
            completedToday,
            totalRevenue,
            currency);
    }

    private static StaffOrderDto MapToDto(Order order)
    {
        return new StaffOrderDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.OrderType,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            order.Currency,
            order.SpecialInstructions,
            order.CreatedAt,
            order.CompletedAt,
            order.CancelledAt,
            order.Items.Select(i => new StaffOrderItemDto(
                i.MenuItemId,
                i.ItemName,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal,
                i.Currency)).ToList());
    }
}