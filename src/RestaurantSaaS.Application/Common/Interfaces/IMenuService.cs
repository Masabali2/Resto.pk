using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Menus.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IMenuService
{
   Task<MenuDto> CreateAsync(Guid restaurantId, CreateMenuRequest request, CancellationToken ct = default);

   Task<PagedResult<MenuDto>> ListByRestaurantAsync(Guid restaurantId, int page, int pageSize, CancellationToken ct = default);

   Task<MenuDetailsDto> GetByIdAsync(Guid menuId, CancellationToken ct = default);

   /// <summary>
   /// Returns the approved platform default menu (the seeded demo catalogue)
   /// with its categories and dishes. Read-only for every tenant.
   /// </summary>
   Task<DefaultMenuDetailsDto> GetDefaultAsync(CancellationToken ct = default);

   /// <summary>
   /// Deep-copies the approved default menu into a restaurant owned by the
   /// authenticated tenant. The copy starts in Draft status.
   /// </summary>
   Task<MenuDto> CopyDefaultAsync(Guid restaurantId, CancellationToken ct = default);

   Task<MenuDto> UpdateAsync(Guid menuId, UpdateMenuRequest request, CancellationToken ct = default);

   Task<MenuDto> SetActiveAsync(Guid menuId, bool isActive, CancellationToken ct = default);

   Task ArchiveAsync(Guid menuId, CancellationToken ct = default);
}
