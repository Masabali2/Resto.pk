using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Orders.DTOS;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Hubs;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Tenancy;

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
    IDateTimeProvider dateTimeProvider,
    IOrderNumberGenerator orderNumberGenerator,
    INotificationService notificationService = null!,
    IHubContext<OrderHub> hubContext = null!) : IStaffOrderService
{
    public async Task<PagedResult<StaffOrderDto>> ListAsync(
        Guid? restaurantId,
        OrderStatus? status,
        string? search,
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(searchTerm) ||
                o.CustomerName.Contains(searchTerm) ||
                o.CustomerPhone.Contains(searchTerm));
        }

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
        var tenantId = await GetActiveTenantIdAsync(
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

        // Publish status change to staff (restaurant group) and to the
        // customer tracking page (per-order group) after successful
        // persistence. The backend is the source of truth; the UI updates
        // only from the confirmed event.
        var statusChangedPayload = new
        {
            orderId,
            orderNumber = order.OrderNumber,
            restaurantId,
            status = order.Status,
            completedAt = order.CompletedAt,
            cancelledAt = order.CancelledAt,
            updatedAt = order.UpdatedAt
        };

        if (notificationService is not null)
        {
            await notificationService.NotifyRestaurantAsync(
                tenantId,
                restaurantId,
                "order.status_changed",
                statusChangedPayload);
        }

        if (hubContext is not null)
        {
            await hubContext.Clients.Group($"order:{orderId}")
                .SendAsync("order.status_changed", new[] { statusChangedPayload }, ct);
        }

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
            .Include(o => o.Items)
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


    public async Task<StaffCreateOrderDto> GetCreateContextAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await GetActiveTenantIdAsync(
            Permissions.OrderView,
            cancellationToken);

        var restaurant = await db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == restaurantId &&
                     r.TenantId == tenantId,
                cancellationToken)
            ?? throw new NotFoundException("Restaurant", restaurantId);

        if (!restaurant.CanAcceptOrders)
        {
            throw new DomainException(
                "RESTAURANT_NOT_ACCEPTING_ORDERS",
                "This restaurant is not currently accepting orders.");
        }

        var menu = await db.Menus
            .AsNoTracking()
            .Where(m =>
                m.RestaurantId == restaurantId &&
                m.TenantId == tenantId &&
                m.IsActive &&
                m.Status == MenuStatus.Published)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Published menu", restaurantId);

        var categories = await db.MenuCategories
            .AsNoTracking()
            .Where(c =>
                c.MenuId == menu.Id &&
                c.IsActive &&
                !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new MenuCategoryDto(
                c.Id,
                c.MenuId,
                c.Name,
                c.Description,
                c.DisplayOrder,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt,
                c.Items.Count(i => i.IsActive && !i.IsDeleted)))
            .ToListAsync(cancellationToken);


        var items = await db.MenuItems
            .AsNoTracking()
            .Where(i =>
                i.Category.MenuId == menu.Id &&
                i.Category.IsActive &&
                !i.Category.IsDeleted &&
                i.IsActive &&
                !i.IsDeleted)
            .OrderBy(i => i.Category.DisplayOrder)
            .ThenBy(i => i.DisplayOrder)
            .ThenBy(i => i.Name)
            .Select(i => new MenuItemDto(
                i.Id,
                i.MenuCategoryId,
                i.Category.MenuId,
                i.Name,
                i.Description,
                i.Price,
                i.Currency,
                i.ImageFileId,
                i.DisplayOrder,
                i.IsAvailable,
                i.IsActive,
                i.CreatedAt,
                i.UpdatedAt,
                i.Category.Name,
                null))
            .ToListAsync(cancellationToken);


        return new StaffCreateOrderDto(
            restaurant.Id,
            restaurant.Name,
            restaurant.TaxRate,
            Money.DefaultCurrency,
            menu.Id,
            menu.Name,
            categories,
            items,
            new[]
            {
            OrderType.Pickup,
            OrderType.DineIn,
            OrderType.Delivery
            },
            PaymentMethod.Cash);
    }

    public async Task<StaffOrderDto> CreateAsync(
        Guid restaurantId,
        RestaurantSaaS.Application.Features.Orders.DTOs.CreateOrderRequest request,
        CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.OrderCreate, ct);
        var tenantId = tenantContext.GetRequiredTenantId();

        var restaurant = await db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Restaurant", restaurantId);

        if (!restaurant.CanAcceptOrders)
        {
            throw new DomainException(
                "RESTAURANT_NOT_ACCEPTING_ORDERS",
                "This restaurant is not currently accepting orders.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new DomainException("ORDER_ITEMS_REQUIRED", "An order must have at least one item.");
        }

        var distinctItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        if (distinctItemIds.Count != request.Items.Count)
        {
            throw new DomainException(
                "DUPLICATE_MENU_ITEM",
                "Duplicate menu item IDs are not allowed. Use quantity instead.");
        }

        var menuItems = await db.MenuItems
            .AsNoTracking()
            .Include(i => i.Category)
            .ThenInclude(c => c.Menu)
            .Where(i => distinctItemIds.Contains(i.Id))
            .ToListAsync(ct);

        var menuItemLookup = menuItems.ToDictionary(mi => mi.Id);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;
        string? currency = null;

        foreach (var itemRequest in request.Items)
        {
            if (!menuItemLookup.TryGetValue(itemRequest.MenuItemId, out var menuItem))
            {
                throw new NotFoundException("MenuItem", itemRequest.MenuItemId);
            }

            if (menuItem.Category is null ||
                menuItem.Category.Menu is null ||
                menuItem.Category.Menu.RestaurantId != restaurantId)
            {
                throw new CrossTenantAccessException();
            }

            if (!menuItem.CanBeOrdered)
            {
                throw new DomainException(
                    "ITEM_NOT_ORDERABLE",
                    $"Menu item '{menuItem.Name}' is no longer available for ordering.");
            }

            var price = Money.New(menuItem.Price, menuItem.Currency);

            if (currency is null)
                currency = price.Currency;
            else if (currency != price.Currency)
            {
                throw new DomainException(
                    "CURRENCY_MISMATCH",
                    "All items in an order must use the same currency.");
            }

            var lineTotal = price.Amount * itemRequest.Quantity;
            subtotal += lineTotal;

            orderItems.Add(OrderItem.CreateSnapshot(
                Guid.NewGuid(),
                itemRequest.MenuItemId,
                menuItem.Name,
                price.Amount,
                itemRequest.Quantity,
                price.Currency));
        }

        var financials = CalculateFinancials(subtotal, restaurant.TaxRate);

        var orderNumber = orderNumberGenerator.Generate();

        var order = Order.Create(
            tenantId,
            restaurantId,
            null,
            orderNumber,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            financials.Subtotal,
            financials.DiscountAmount,
            financials.TaxAmount,
            financials.TotalAmount,
            currency ?? Money.DefaultCurrency,
            request.SpecialInstructions,
            request.OrderType,
            orderItems);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Publish new-order event so the restaurant's staff dashboard
        // updates without a page reload.
        if (notificationService is not null)
        {
            await notificationService.NotifyRestaurantAsync(
            tenantId,
            restaurantId,
            "order.created",
            new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                restaurantId,
                status = order.Status,
                orderType = order.OrderType,
                customerName = order.CustomerName,
                customerPhone = order.CustomerPhone,
                customerEmail = order.CustomerEmail,
                subtotal = order.Subtotal,
                taxAmount = order.TaxAmount,
                totalAmount = order.TotalAmount,
                currency = order.Currency,
                specialInstructions = order.SpecialInstructions,
                createdAt = order.CreatedAt,
                items = orderItems.Select(oi => new
                {
                    oi.MenuItemId,
                    oi.ItemName,
                    oi.UnitPrice,
                    oi.Quantity,
                    oi.LineTotal,
                    oi.Currency
                })
            });
        }

        return MapToDto(order);
    }

    private static (decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount) CalculateFinancials(
        decimal subtotal,
        decimal taxRate)
    {
        var discountAmount = 0m;
        var taxAmount = Math.Round(subtotal * taxRate / 100m, 4, MidpointRounding.AwayFromZero);
        var totalAmount = subtotal - discountAmount + taxAmount;

        return (subtotal, discountAmount, taxAmount, totalAmount);
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