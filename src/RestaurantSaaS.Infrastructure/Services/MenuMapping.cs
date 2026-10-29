using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;
using RestaurantSaaS.Application.Features.Menus.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Services;

internal static class MenuMapping
{
   public static MenuDto ToDto(Menu menu) => new(
       menu.Id,
       menu.TenantId,
       menu.RestaurantId,
       menu.Name,
       menu.Description,
       menu.IsActive,
       menu.Status.ToString(),
       menu.CreatedAt,
       menu.UpdatedAt);

   public static MenuDetailsDto ToDetailsDto(Menu menu) => new(
       menu.Id,
       menu.TenantId,
       menu.RestaurantId,
       menu.Name,
       menu.Description,
       menu.IsActive,
       menu.Status.ToString(),
       menu.CreatedAt,
       menu.UpdatedAt,
       menu.Categories
           .OrderBy(c => c.DisplayOrder)
           .ThenBy(c => c.Name)
           .Select(ToDto)
           .ToList());

   public static MenuCategoryDto ToDto(MenuCategory category, int itemCount = 0) => new(
       category.Id,
       category.MenuId,
       category.Name,
       category.Description,
       category.DisplayOrder,
       category.IsActive,
       category.CreatedAt,
       category.UpdatedAt,
       itemCount);

   public static MenuItemDto ToDto(MenuItem item, Guid menuId, string? categoryName = null) => new(
       item.Id,
       item.MenuCategoryId,
       menuId,
       item.Name,
       item.Description,
       item.Price,
       item.Currency,
       item.ImageFileId,
       item.DisplayOrder,
       item.IsAvailable,
       item.IsActive,
       item.CreatedAt,
       item.UpdatedAt,
       categoryName);
}
