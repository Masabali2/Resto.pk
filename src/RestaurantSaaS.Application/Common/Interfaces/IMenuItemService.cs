using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IMenuItemService
{
    Task<MenuItemDto> CreateAsync(Guid categoryId, CreateMenuItemRequest request, CancellationToken ct = default);

    Task<PagedResult<MenuItemDto>> ListByCategoryAsync(Guid categoryId, int page, int pageSize, CancellationToken ct = default);

    Task<PagedResult<MenuItemDto>> ListByMenuAsync(Guid menuId, int page, int pageSize, CancellationToken ct = default);

    Task<MenuItemDto> GetByIdAsync(Guid itemId, CancellationToken ct = default);

    Task<MenuItemDto> UpdateAsync(Guid itemId, UpdateMenuItemRequest request, CancellationToken ct = default);

    Task<MenuItemDto> SetAvailabilityAsync(Guid itemId, bool isAvailable, CancellationToken ct = default);

    Task<MenuItemDto> SetActiveAsync(Guid itemId, bool isActive, CancellationToken ct = default);

    Task SoftDeleteAsync(Guid itemId, CancellationToken ct = default);
}
