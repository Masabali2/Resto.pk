using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;

namespace RestaurantSaaS.Application.Features.Restaurants;

/// <summary>
/// Restaurant management use cases (Tenant 1 → M Restaurants). All operations
/// are scoped to the resolved tenant context; cross-tenant access fails closed.
/// </summary>
public interface IRestaurantService
{
    Task<RestaurantDto> CreateAsync(CreateRestaurantRequest request, CancellationToken ct = default);

    Task<PagedResult<RestaurantDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Returns the restaurant only if it belongs to the current tenant; otherwise throws NotFoundException (no existence leakage).</summary>
    Task<RestaurantDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RestaurantDto> UpdateAsync(Guid id, UpdateRestaurantRequest request, CancellationToken ct = default);

    Task<RestaurantDto> ChangeStatusAsync(Guid id, string status, CancellationToken ct = default);

    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
