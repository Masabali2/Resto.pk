//using RestaurantSaaS.Application.Common.Models;
//using RestaurantSaaS.Application.Features.Menus.DTOs;

//namespace RestaurantSaaS.Application.Features.Menus;

//public interface IMenuService
//{
//    Task<MenuDto> CreateAsync(Guid restaurantId, CreateMenuRequest request, CancellationToken ct = default);

//    Task<PagedResult<MenuDto>> ListByRestaurantAsync(Guid restaurantId, int page, int pageSize, CancellationToken ct = default);

//    Task<MenuDetailsDto> GetByIdAsync(Guid menuId, CancellationToken ct = default);

//    Task<MenuDto> UpdateAsync(Guid menuId, UpdateMenuRequest request, CancellationToken ct = default);

//    Task<MenuDto> SetActiveAsync(Guid menuId, bool isActive, CancellationToken ct = default);

//    Task ArchiveAsync(Guid menuId, CancellationToken ct = default);
//}
