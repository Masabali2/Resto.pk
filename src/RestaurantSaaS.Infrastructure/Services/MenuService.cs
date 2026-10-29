using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.Menus.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class MenuService(ApplicationDbContext db, TenantAccessGuard accessGuard) : IMenuService
{
   public async Task<MenuDto> CreateAsync(Guid restaurantId, CreateMenuRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCreate, ct);
       var restaurant = await LoadOwnedRestaurantAsync(restaurantId, tenantId, ct);

       var menu = Menu.Create(tenantId, restaurant.Id, request.Name, request.Description);
       db.Menus.Add(menu);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(menu);
   }

   public async Task<PagedResult<MenuDto>> ListByRestaurantAsync(Guid restaurantId, int page, int pageSize, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuView, ct);
       await LoadOwnedRestaurantAsync(restaurantId, tenantId, ct);

       page = Math.Max(1, page);
       pageSize = Math.Clamp(pageSize, 1, 100);

       var query = db.Menus.AsNoTracking()
           .Where(m => m.RestaurantId == restaurantId && m.Status != MenuStatus.Archived)
           .OrderBy(m => m.Name);

       var total = await query.CountAsync(ct);
       var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

       return new PagedResult<MenuDto>
       {
           Items = items.Select(MenuMapping.ToDto).ToList(),
           Page = page,
           PageSize = pageSize,
           TotalCount = total
       };
   }

   public async Task<MenuDetailsDto> GetByIdAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuView, ct);

       var menu = await db.Menus.AsNoTracking()
           .FirstOrDefaultAsync(m => m.Id == menuId, ct)
           ?? throw new NotFoundException("Menu", menuId);

       EnsureOwned(menu, tenantId);

       var categories = await db.MenuCategories.AsNoTracking()
           .Where(c => c.MenuId == menuId)
           .OrderBy(c => c.DisplayOrder)
           .ThenBy(c => c.Name)
           .Select(c => new MenuCategoryDto(
               c.Id,
               c.MenuId,
               c.Name,
               c.Description,
               c.DisplayOrder,
               c.IsActive,
               c.CreatedAt,
               c.UpdatedAt,
               c.Items.Count))
           .ToListAsync(ct);

       return new MenuDetailsDto(
           menu.Id,
           menu.TenantId,
           menu.RestaurantId,
           menu.Name,
           menu.Description,
           menu.IsActive,
           menu.Status.ToString(),
           menu.CreatedAt,
           menu.UpdatedAt,
           categories);
   }

   public async Task<MenuDto> UpdateAsync(Guid menuId, UpdateMenuRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuUpdate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       menu.EnsureEditable();
       menu.Update(request.Name, request.Description, isActive: null);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(menu);
   }

   public async Task<MenuDto> SetActiveAsync(Guid menuId, bool isActive, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuUpdate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       menu.SetActive(isActive);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(menu);
   }

   public async Task<MenuDto> PublishAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuUpdate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       var categoryCount = await db.MenuCategories
           .CountAsync(c => c.MenuId == menu.Id && !c.IsDeleted && c.IsActive, ct);
       menu.Publish(categoryCount);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(menu);
   }

   public async Task<MenuDto> UnpublishAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuUpdate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       if (menu.Status != MenuStatus.Published)
           throw new ConflictException("Only a published menu can be unpublished.");
       menu.Unpublish();
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(menu);
   }

   public async Task ArchiveAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuDelete, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       menu.Archive();
       await db.SaveChangesAsync(ct);
   }

   private async Task<Restaurant> LoadOwnedRestaurantAsync(Guid restaurantId, Guid tenantId, CancellationToken ct)
   {
       var restaurant = await db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
           ?? throw new NotFoundException("Restaurant", restaurantId);

       if (restaurant.TenantId != tenantId)
           throw new NotFoundException("Restaurant", restaurantId);

       return restaurant;
   }

   private async Task<Menu> LoadOwnedMenuAsync(Guid menuId, Guid tenantId, CancellationToken ct)
   {
       var menu = await db.Menus.FirstOrDefaultAsync(m => m.Id == menuId, ct)
           ?? throw new NotFoundException("Menu", menuId);

       EnsureOwned(menu, tenantId);
       return menu;
   }

   private static void EnsureOwned(Menu menu, Guid tenantId)
   {
       if (menu.TenantId != tenantId)
           throw new NotFoundException("Menu", menu.Id);
   }
}
