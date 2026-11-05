using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Public restaurant resolution for anonymous customer ordering.
///
/// Resolves a restaurant through its published website slug and verifies that
/// ordering is permitted. All checks are server-side; the caller supplies only
/// a slug.
/// </summary>
public sealed class PublicRestaurantService(
    ApplicationDbContext db) : IPublicRestaurantService
{
    public async Task<PublicOrderingRestaurantDto?> GetValidRestaurantForOrderingAsync(
        string websiteSlug,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(websiteSlug))
            return null;

        var normalizedSlug = Domain.Entities.Websites.Website.NormalizeSlug(websiteSlug);

        // Resolve website → restaurant → tenant chain entirely server-side.
        var website = await db.Websites.AsNoTracking()
            .Include(w => w.Restaurant)
            .ThenInclude(r => r.Tenant)
            .Where(w => w.Slug == normalizedSlug
                        && w.IsPublished
                        && w.Status == WebsiteStatus.Published)
            .FirstOrDefaultAsync(ct);

        if (website is null)
            return null;

        var restaurant = website.Restaurant;
        var tenant = restaurant.Tenant;

        // Tenant must be active and not deleted.
        if (tenant is null || tenant.IsDeleted || tenant.Status != TenantStatus.Active)
            return null;

        // Restaurant must accept orders.
        if (!restaurant.CanAcceptOrders)
            return null;

        // Subscription entitlement for public ordering.
        var entitled = await IsSubscriptionEntitledAsync(tenant.Id, ct);
        if (!entitled)
            return null;

        return new PublicOrderingRestaurantDto(
            restaurant.Id,
            tenant.Id,
            website.Id,
            restaurant.Name,
            website.Slug,
            restaurant.City,
            restaurant.Country,
            restaurant.TimeZone,
            restaurant.TaxRate);
    }

    private async Task<bool> IsSubscriptionEntitledAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var subscription = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Status,
                s.StartDate,
                s.EndDate
            })
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return false;

        return subscription.Status is
                Domain.Enums.SubscriptionStatus.Trial
                or Domain.Enums.SubscriptionStatus.Active
                or Domain.Enums.SubscriptionStatus.PastDue
            && subscription.StartDate <= now
            && (subscription.EndDate is null || subscription.EndDate > now);
    }
}
