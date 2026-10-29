using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class MenuCategoryService(ApplicationDbContext db, TenantAccessGuard accessGuard) : IMenuCategoryService
{
   public async Task<MenuCategoryDto> CreateAsync(Guid menuId, CreateMenuCategoryRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryCreate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       menu.EnsureEditable();

       var displayOrder = request.DisplayOrder
           ?? await NextDisplayOrderAsync(menuId, ct);

       var category = MenuCategory.Create(menu.Id, request.Name, request.Description, displayOrder);
       db.MenuCategories.Add(category);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(category);
   }

   public async Task<IReadOnlyList<MenuCategoryDto>> ListByMenuAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryView, ct);
       await LoadOwnedMenuAsync(menuId, tenantId, ct);

       return await db.MenuCategories.AsNoTracking()
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
   }

   public async Task<MenuCategoryDto> GetByIdAsync(Guid categoryId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryView, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct);
       var itemCount = await db.MenuItems.AsNoTracking()
           .CountAsync(i => i.MenuCategoryId == categoryId, ct);
       return MenuMapping.ToDto(category, itemCount);
   }

   public async Task<MenuCategoryDto> UpdateAsync(Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryUpdate, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct, trackMenu: true);
       category.Menu.EnsureEditable();
       category.Update(request.Name, request.Description);
       if (request.DisplayOrder.HasValue)
           category.SetDisplayOrder(request.DisplayOrder.Value);
       await db.SaveChangesAsync(ct);
       var itemCount = await db.MenuItems.AsNoTracking()
           .CountAsync(i => i.MenuCategoryId == categoryId, ct);
       return MenuMapping.ToDto(category, itemCount);
   }

   public async Task<MenuCategoryDto> SetActiveAsync(Guid categoryId, bool isActive, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryUpdate, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct, trackMenu: true);
       category.Menu.EnsureEditable();
       category.SetActive(isActive);
       await db.SaveChangesAsync(ct);
       var itemCount = await db.MenuItems.AsNoTracking()
           .CountAsync(i => i.MenuCategoryId == categoryId, ct);
       return MenuMapping.ToDto(category, itemCount);
   }

   public async Task<IReadOnlyList<MenuCategoryDto>> ReorderAsync(Guid menuId, ReorderMenuCategoriesRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryUpdate, ct);
       var menu = await LoadOwnedMenuAsync(menuId, tenantId, ct);
       menu.EnsureEditable();

       var categories = await db.MenuCategories
           .Where(c => c.MenuId == menuId)
           .ToListAsync(ct);

       var existingIds = categories.Select(c => c.Id).ToHashSet();
       var requestedIds = request.OrderedCategoryIds;

       if (requestedIds.Count != existingIds.Count || requestedIds.Any(id => !existingIds.Contains(id)))
           throw new DomainException("CATEGORY_REORDER_MISMATCH",
               "The ordered category list must contain every category of this menu exactly once.");

       await using var tx = await db.Database.BeginTransactionAsync(ct);
       try
       {
           for (var i = 0; i < requestedIds.Count; i++)
           {
               var category = categories.First(c => c.Id == requestedIds[i]);
               category.SetDisplayOrder(i);
           }

           await db.SaveChangesAsync(ct);
           await tx.CommitAsync(ct);
       }
       catch
       {
           await tx.RollbackAsync(ct);
           throw;
       }

       var itemCounts = await db.MenuItems.AsNoTracking()
           .Where(i => i.Category.MenuId == menuId)
           .GroupBy(i => i.MenuCategoryId)
           .Select(group => new { CategoryId = group.Key, Count = group.Count() })
           .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

       return categories
           .OrderBy(c => c.DisplayOrder)
           .ThenBy(c => c.Name)
           .Select(c => MenuMapping.ToDto(c, itemCounts.GetValueOrDefault(c.Id)))
           .ToList();
   }

   public async Task DeleteAsync(Guid categoryId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuCategoryDelete, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct, trackMenu: true);
       category.Menu.EnsureEditable();

       var hasItems = await db.MenuItems.IgnoreQueryFilters()
           .AnyAsync(i => i.MenuCategoryId == categoryId, ct);
       if (hasItems)
           throw new ConflictException("A category that still has menu items cannot be deleted.");

       db.MenuCategories.Remove(category);
       await db.SaveChangesAsync(ct);
   }

   private async Task<int> NextDisplayOrderAsync(Guid menuId, CancellationToken ct)
   {
       var max = await db.MenuCategories
           .Where(c => c.MenuId == menuId)
           .Select(c => (int?)c.DisplayOrder)
           .MaxAsync(ct);
       return (max ?? -1) + 1;
   }

   private async Task<Menu> LoadOwnedMenuAsync(Guid menuId, Guid tenantId, CancellationToken ct)
   {
       var menu = await db.Menus.FirstOrDefaultAsync(m => m.Id == menuId, ct)
           ?? throw new NotFoundException("Menu", menuId);

       if (menu.TenantId != tenantId)
           throw new NotFoundException("Menu", menuId);

       return menu;
   }

   private async Task<MenuCategory> LoadOwnedCategoryAsync(Guid categoryId, Guid tenantId, CancellationToken ct, bool trackMenu = false)
   {
       var query = db.MenuCategories.Include(c => c.Menu).AsQueryable();
       if (!trackMenu)
           query = query.AsNoTracking();

       var category = await query.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
           ?? throw new NotFoundException("MenuCategory", categoryId);

       if (category.Menu.TenantId != tenantId)
           throw new NotFoundException("MenuCategory", categoryId);

       return category;
   }
}
