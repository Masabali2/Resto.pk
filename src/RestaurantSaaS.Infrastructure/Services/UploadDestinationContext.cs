
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class UploadDestinationContext : IUploadDestinationContext
{
    private readonly ApplicationDbContext _db;

    public Guid TenantId { get; }

    public UploadDestinationContext(
        ApplicationDbContext db,
        ITenantContext tenantContext)
    {
        _db = db;
        TenantId = tenantContext.TenantId
            ?? throw new InvalidOperationException(
                "A tenant must be resolved before uploading an asset.");
    }

    public Task<Website?> FindWebsiteAsync(
        Guid websiteId,
        CancellationToken cancellationToken) =>
        _db.Websites.FirstOrDefaultAsync(
            w => w.Id == websiteId && w.TenantId == TenantId,
            cancellationToken);

    public Task<Restaurant?> FindRestaurantAsync(
        Guid restaurantId,
        CancellationToken cancellationToken) =>
        _db.Restaurants.FirstOrDefaultAsync(
            r => r.Id == restaurantId &&
                 r.TenantId == TenantId &&
                 !r.IsDeleted,
            cancellationToken);

    public Task<MenuItem?> FindMenuItemAsync(
        Guid menuItemId,
        CancellationToken cancellationToken) =>
        _db.MenuItems
            .Include(i => i.Category)
            .ThenInclude(c => c.Menu)
            .FirstOrDefaultAsync(
                i => i.Id == menuItemId && !i.IsDeleted &&
                     i.Category.Menu.TenantId == TenantId,
                cancellationToken);

    public Task<bool> IsActiveMemberAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        _db.StaffMemberships.AnyAsync(
            m => m.UserId == userId &&
                 m.TenantId == TenantId &&
                 m.Status == MembershipStatus.Active,
            cancellationToken);

    public Task<List<RestaurantAsset>> FindRestaurantAssetsAsync(
        Guid restaurantId,
        CancellationToken cancellationToken) =>
        _db.RestaurantAssets
            .Where(a => a.RestaurantId == restaurantId)
            .ToListAsync(cancellationToken);

    public void AddRestaurantAsset(RestaurantAsset asset) =>
        _db.RestaurantAssets.Add(asset);
}
