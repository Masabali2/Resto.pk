using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;
using System.Collections.Generic;

namespace RestaurantSaaS.Infrastructure.Hubs;

/// <summary>
/// Real-time order delivery hub.
///
/// Staff (authenticated) subscribe to their restaurant group to receive
/// newly created orders and status changes. Anonymous customers subscribe
/// to a specific order group using a validated tracking token so the
/// customer tracking page receives live status updates without polling.
///
/// Group naming convention (no tracking tokens ever appear in group names):
///   tenant:{tenantId}:restaurant:{restaurantId}   - staff of a restaurant
///   order:{orderId}                               - customers tracking an order
///
/// Business logic (validation, persistence) stays in the application
/// layer; this hub only manages group membership and delivery.
/// </summary>
[Authorize]
public sealed class OrderHub : Hub
{
    public const string HubPath = "hubs/orders";

    private readonly IPublicRestaurantService _restaurants;
    private readonly IPublicOrderService _orders;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantMembershipService _membershipService;
    private readonly ApplicationDbContext _db;

    public OrderHub(
        IPublicRestaurantService restaurants,
        IPublicOrderService orders,
        ITenantContext tenantContext,
        ICurrentUserService currentUser,
        ITenantMembershipService membershipService,
        ApplicationDbContext db)
    {
        _restaurants = restaurants;
        _orders = orders;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _membershipService = membershipService;
        _db = db;
    }

    /// <summary>Authenticated staff joins their restaurant's order group.</summary>
    [Authorize(Policy = IdentityPolicyNames.PermissionPrefix + Permissions.OrderView)]
    public async Task SubscribeToRestaurant(Guid restaurantId)
    {
        var userId = _currentUser.UserId ?? throw new HubException("NOT_AUTHENTICATED");
        var tenantId = _tenantContext.GetRequiredTenantId();

        var membership = await _membershipService.GetMembershipAsync(userId, tenantId, Context.ConnectionAborted)
            ?? throw new HubException("NOT_A_MEMBER");

        if (membership.Status != (int)MembershipStatus.Active)
            throw new HubException("MEMBERSHIP_NOT_ACTIVE");

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(Permissions.OrderView))
        {
            throw new HubException("INSUFFICIENT_PERMISSIONS");
        }

        var restaurantExists = await _db.Restaurants.AsNoTracking()
            .AnyAsync(r => r.Id == restaurantId && r.TenantId == tenantId, Context.ConnectionAborted);
        if (!restaurantExists)
            throw new HubException("RESTAURANT_NOT_FOUND");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Restaurant(tenantId, restaurantId));
    }

    /// <summary>
    /// Anonymous customer joins a specific order group after the tracking
    /// token is validated server-side. The slug resolves the restaurant and
    /// the token proves the customer knows the order.
    /// </summary>
    [AllowAnonymous]
    public async Task SubscribeToOrder(string slug, string trackingToken)
    {
        var restaurant = await _restaurants.GetValidRestaurantForOrderingAsync(slug, Context.ConnectionAborted)
            ?? throw new HubException("RESTAURANT_NOT_FOUND");

        var orderId = await _orders.GetOrderIdByTrackingTokenAsync(restaurant.Id, trackingToken, Context.ConnectionAborted)
            ?? throw new HubException("ORDER_NOT_FOUND");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Order(orderId));
    }

    public override Task OnDisconnectedAsync(Exception? exception) => base.OnDisconnectedAsync(exception);
}

internal static class GroupNames
{
    public static string Restaurant(Guid tenantId, Guid restaurantId) =>
        $"tenant:{tenantId}:restaurant:{restaurantId}";

    public static string Order(Guid orderId) => $"order:{orderId}";
}
