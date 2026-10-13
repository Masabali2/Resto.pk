using RestaurantSaaS.Application.Features.MenuCategories.DTOs;

namespace RestaurantSaaS.Application.Features.MenuCategories;

public interface IMenuCategoryService
{
    Task<MenuCategoryDto> CreateAsync(Guid menuId, CreateMenuCategoryRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MenuCategoryDto>> ListByMenuAsync(Guid menuId, CancellationToken ct = default);

    Task<MenuCategoryDto> GetByIdAsync(Guid categoryId, CancellationToken ct = default);

    Task<MenuCategoryDto> UpdateAsync(Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken ct = default);

    Task<MenuCategoryDto> SetActiveAsync(Guid categoryId, bool isActive, CancellationToken ct = default);

    Task<IReadOnlyList<MenuCategoryDto>> ReorderAsync(Guid menuId, ReorderMenuCategoriesRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid categoryId, CancellationToken ct = default);
}
