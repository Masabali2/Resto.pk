using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Restaurant management. Tenant scope is taken EXCLUSIVELY from ITenantContext
/// (server-resolved). Requests contain no TenantId field, so a client can never
/// target or reassign a tenant through body/route/query. Reads go through the
/// EF tenant global query filter AND explicit ownership checks (defense in depth).
/// </summary>
public sealed class RestaurantService(
    ApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService) : IRestaurantService
{
    public async Task<RestaurantDto> CreateAsync(CreateRestaurantRequest request, CancellationToken ct = default)
    {
        var tenantId = await GetActiveTenantIdAsync(Permissions.RestaurantCreate, ct);

        var address = Address.Create(
            request.Address.Line1, request.Address.City, request.Address.Country,
            request.Address.Line2, request.Address.PostalCode);

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
            phone = PhoneNumber.Parse(request.Phone);

        EmailAddress? email = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
            email = EmailAddress.Parse(request.Email);

        var timeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Karachi" : request.TimeZone!;
        ValidateTimeZone(timeZone);

        var restaurant = Restaurant.Create(tenantId, request.Name, address, phone, email, request.Description, timeZone);

        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync(ct); // new restaurants start Draft — safe default

        return ToDto(restaurant);
    }

    public async Task<PagedResult<RestaurantDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantView, ct);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Restaurants.AsNoTracking().OrderBy(r => r.Name);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RestaurantDto>
        {
            Items = items.Select(ToDtoStatic).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<RestaurantDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantView, ct);

        // Query filter already restricts to current tenant; a cross-tenant id
        // therefore simply "does not exist" here → 404, no existence leakage.
        var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Restaurant", id);

        return ToDto(restaurant);
    }

    public async Task<RestaurantDto> UpdateAsync(Guid id, UpdateRestaurantRequest request, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantUpdate, ct);

        var restaurant = await LoadOwnedAsync(id, ct);

        Address? address = null;
        if (request.Address is not null)
        {
            address = Address.Create(
                request.Address.Line1, request.Address.City, request.Address.Country,
                request.Address.Line2, request.Address.PostalCode);
        }

        PhoneNumber? phone = null;
        if (request.Phone is not null)
            phone = PhoneNumber.Parse(request.Phone);

        EmailAddress? email = null;
        if (request.Email is not null)
            email = EmailAddress.Parse(request.Email);

        if (request.TimeZone is not null)
            ValidateTimeZone(request.TimeZone);

        restaurant.UpdateProfile(request.Name, request.Description, address, phone, email, request.TimeZone);
        await db.SaveChangesAsync(ct);

        return ToDto(restaurant);
    }

    public async Task<RestaurantDto> ChangeStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        // Deactivation requires its own permission; other transitions require Update.
        var deactivate = Enum.TryParse<RestaurantStatus>(status, ignoreCase: false, out var parsed) &&
                         parsed is RestaurantStatus.TemporarilyClosed or RestaurantStatus.Closed;

        await GetActiveTenantIdAsync(
            deactivate ? Permissions.RestaurantDeactivate : Permissions.RestaurantUpdate, ct);

        var restaurant = await LoadOwnedAsync(id, ct);

        if (!Enum.TryParse<RestaurantStatus>(status, ignoreCase: false, out var newStatus))
            throw new DomainException("RESTAURANT_STATUS_INVALID", "Unknown restaurant status.");

        restaurant.ChangeStatus(newStatus); // domain enforces Closed = terminal
        await db.SaveChangesAsync(ct);

        return ToDto(restaurant);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        await GetActiveTenantIdAsync(Permissions.RestaurantUpdate, ct);

        var restaurant = await LoadOwnedAsync(id, ct);

        restaurant.SoftDelete();
        await db.SaveChangesAsync(ct); // historical orders/menus remain untouched
    }

    // ---------------- helpers ----------------

    /// <summary>
    /// Resolves + validates the acting tenant: context present, membership ACTIVE,
    /// role grants the required permission, and tenant operational. Every check is
    /// server-side; nothing trusts the client.
    /// </summary>
    private async Task<Guid> GetActiveTenantIdAsync(string requiredPermission, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new NotAuthenticatedException();
        var tenantId = tenantContext.GetRequiredTenantId();

        var membership = await membershipService.GetMembershipAsync(userId, tenantId, ct)
            ?? throw new CrossTenantAccessException();

        if (membership.Status != (int)MembershipStatus.Active)
            throw new CrossTenantAccessException();

        if (!Permissions.RolePermissions.TryGetValue(membership.RoleName, out var perms) ||
            !perms.Contains(requiredPermission))
        {
            throw new ForbiddenException($"Your role '{membership.RoleName}' cannot perform this operation.");
        }

        // Tenant must be operational for mutating operations (view allowed while suspended is
        // intentionally NOT granted here either — suspended tenants are frozen).
        var tenantOperational = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active && !t.IsDeleted, ct);
        if (!tenantOperational)
            throw new DomainException("TENANT_NOT_ACTIVE", "This tenant is not active.");

        return tenantId;
    }

    private async Task<Restaurant> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Restaurant", id);

        // Explicit ownership validation — query filters alone are not trusted.
        if (restaurant.TenantId != tenantId)
            throw new CrossTenantAccessException();

        return restaurant;
    }

    private static void ValidateTimeZone(string timeZone)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException("RESTAURANT_TIMEZONE_INVALID", $"Time zone '{timeZone}' is not a valid IANA identifier.");
        }
    }

    private static RestaurantDto ToDto(Restaurant r) => ToDtoStatic(r);

    private static RestaurantDto ToDtoStatic(Restaurant r) => new(
        r.Id, r.TenantId, r.Name, r.Description, r.Phone, r.Email,
        r.AddressLine1, r.AddressLine2, r.City, r.Country, r.TimeZone,
        r.Status.ToString(), r.CreatedAt, r.UpdatedAt);
}
