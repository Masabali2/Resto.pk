using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;
using RestaurantSaaS.Application.Features.Menus.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Seed;

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

   public async Task<DefaultMenuDetailsDto> GetDefaultAsync(CancellationToken ct = default)
   {
       await accessGuard.EnsurePermissionAsync(Permissions.MenuView, ct);

       var source = await LoadDefaultMenuSourceAsync(ct);

       var categoryNames = source.Categories.ToDictionary(c => c.Id, c => c.Name);
       var itemsByCategory = source.Items
           .GroupBy(i => i.MenuCategoryId)
           .ToDictionary(
               group => group.Key,
               group => (IReadOnlyList<MenuItemDto>)group
                   .Select(item => MenuMapping.ToDto(
                       item,
                       source.Menu.Id,
                       categoryNames.GetValueOrDefault(item.MenuCategoryId)))
                   .ToList());

       var categories = source.Categories
           .Select(category => new DefaultMenuCategoryDto(
               category.Id,
               category.Name,
               category.Description,
               category.DisplayOrder,
               itemsByCategory.TryGetValue(category.Id, out var items)
                   ? items
                   : Array.Empty<MenuItemDto>()))
           .ToList();

       return new DefaultMenuDetailsDto(
           source.Menu.Id,
           source.Menu.Name,
           source.Menu.Description,
           source.Menu.Status.ToString(),
           categories.Count,
           source.Items.Count,
           categories);
   }

   public async Task<MenuDto> CopyDefaultAsync(Guid restaurantId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCreate, ct);
       var restaurant = await LoadOwnedRestaurantAsync(restaurantId, tenantId, ct);

       await using var transaction = await db.Database.BeginTransactionAsync(ct);

       Menu copy;

       try
       {
           var source = await LoadDefaultMenuSourceAsync(ct);

           // The copy is always a new Draft menu owned by the acting tenant.
           copy = Menu.Create(tenantId, restaurant.Id, source.Menu.Name, source.Menu.Description);
           db.Menus.Add(copy);

           var copiedCategoryBySourceId = new Dictionary<Guid, MenuCategory>(source.Categories.Count);

           foreach (var category in source.Categories)
           {
               var copiedCategory = MenuCategory.Create(
                   copy.Id,
                   category.Name,
                   category.Description,
                   category.DisplayOrder);

               copiedCategory.SetActive(category.IsActive);

               db.MenuCategories.Add(copiedCategory);
               copiedCategoryBySourceId[category.Id] = copiedCategory;
           }

           foreach (var item in source.Items)
           {
               if (!copiedCategoryBySourceId.TryGetValue(item.MenuCategoryId, out var copiedCategory))
                   continue;

               // Image metadata is tenant-owned, so a cross-tenant copy must not
               // retain the source reference. The seeded default menu has no
               // images, and destination tenants upload their own.
               var copiedItem = MenuItem.Create(
                   copiedCategory.Id,
                   item.Name,
                   Money.New(item.Price, item.Currency),
                   item.Description,
                   imageFileId: null,
                   item.DisplayOrder);

               copiedItem.SetAvailability(item.IsAvailable);
               copiedItem.SetActive(item.IsActive);
               db.MenuItems.Add(copiedItem);
           }

           await db.SaveChangesAsync(ct);
           await transaction.CommitAsync(ct);
       }
       catch
       {
           await transaction.RollbackAsync(CancellationToken.None);
           throw;
       }

       return MenuMapping.ToDto(copy);
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

   /// <summary>
   /// Loads the single approved default menu. The tenant query filter is
   /// bypassed only for this exact seeded identifier, and the result is then
   /// checked against the demo tenant, restaurant and availability rules.
   /// Categories and items are loaded with two additional set-based queries,
   /// so no N+1 traversal is required.
   /// </summary>
   private async Task<DefaultMenuSource> LoadDefaultMenuSourceAsync(CancellationToken ct)
   {
       var menu = await db.Menus
           .IgnoreQueryFilters()
           .AsNoTracking()
           .FirstOrDefaultAsync(m => m.Id == DemoMenuSeeder.DemoMenuId, ct)
           ?? throw new NotFoundException("Default menu", DemoMenuSeeder.DemoMenuId);

       EnsureApprovedDefaultMenu(menu);

       var categories = await db.MenuCategories
           .IgnoreQueryFilters()
           .AsNoTracking()
           .Where(c => c.MenuId == menu.Id && !c.IsDeleted)
           .OrderBy(c => c.DisplayOrder)
           .ThenBy(c => c.Name)
           .ToListAsync(ct);

       var items = await db.MenuItems
           .IgnoreQueryFilters()
           .AsNoTracking()
           .Where(i =>
               i.Category.MenuId == menu.Id &&
               !i.Category.IsDeleted &&
               !i.IsDeleted)
           .OrderBy(i => i.Category.DisplayOrder)
           .ThenBy(i => i.DisplayOrder)
           .ThenBy(i => i.Name)
           .ToListAsync(ct);

       return new DefaultMenuSource(menu, categories, items);
   }

   private static void EnsureApprovedDefaultMenu(Menu menu)
   {
       var isApprovedSource =
           menu.Id == DemoMenuSeeder.DemoMenuId &&
           menu.TenantId == DemoMenuSeeder.DemoTenantId &&
           menu.RestaurantId == DemoMenuSeeder.DemoRestaurantId &&
           menu.Status != MenuStatus.Archived &&
           menu.IsActive;

       if (!isApprovedSource)
           throw new NotFoundException("Default menu", DemoMenuSeeder.DemoMenuId);
   }

   private sealed record DefaultMenuSource(
       Menu Menu,
       IReadOnlyList<MenuCategory> Categories,
       IReadOnlyList<MenuItem> Items);

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
