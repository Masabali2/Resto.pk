using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class MenuItemService(ApplicationDbContext db, TenantAccessGuard accessGuard) : IMenuItemService
{
   public async Task<MenuItemDto> CreateAsync(Guid categoryId, CreateMenuItemRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemCreate, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct);
       category.Menu.EnsureEditable();

       if (request.ImageFileId.HasValue)
           await EnsureUsableImageAsync(request.ImageFileId.Value, tenantId, ct);

       var displayOrder = request.DisplayOrder
           ?? await NextDisplayOrderAsync(categoryId, ct);

       var price = Money.New(request.Price, string.IsNullOrWhiteSpace(request.Currency) ? Money.DefaultCurrency : request.Currency);
       var item = MenuItem.Create(category.Id, request.Name, price, request.Description, request.ImageFileId, displayOrder);
       db.MenuItems.Add(item);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(item, category.MenuId, category.Name);
   }

   public async Task<PagedResult<MenuItemDto>> ListByCategoryAsync(Guid categoryId, int page, int pageSize, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemView, ct);
       var category = await LoadOwnedCategoryAsync(categoryId, tenantId, ct, track: false);

       page = Math.Max(1, page);
       pageSize = Math.Clamp(pageSize, 1, 100);

       var query = db.MenuItems.AsNoTracking()
           .Where(i => i.MenuCategoryId == categoryId)
           .OrderBy(i => i.DisplayOrder)
           .ThenBy(i => i.Name);

       var total = await query.CountAsync(ct);
       var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

       return new PagedResult<MenuItemDto>
       {
           Items = items.Select(i => MenuMapping.ToDto(i, category.MenuId, category.Name)).ToList(),
           Page = page,
           PageSize = pageSize,
           TotalCount = total
       };
   }

   public async Task<PagedResult<MenuItemDto>> ListByMenuAsync(Guid menuId, MenuItemListQuery query, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemView, ct);
       await LoadOwnedMenuAsync(menuId, tenantId, ct);

       var page = Math.Max(1, query.Page);
       var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);

       var itemsQuery = db.MenuItems.AsNoTracking()
           .Where(i => i.Category.MenuId == menuId);

       var search = query.Search?.Trim();
       if (!string.IsNullOrEmpty(search))
       {
           itemsQuery = itemsQuery.Where(i =>
               i.Name.Contains(search) ||
               (i.Description != null && i.Description.Contains(search)));
       }

       if (query.CategoryId is { } categoryId)
           itemsQuery = itemsQuery.Where(i => i.MenuCategoryId == categoryId);

       if (query.IsAvailable is { } isAvailable)
           itemsQuery = itemsQuery.Where(i => i.IsAvailable == isAvailable);

       var descending = string.Equals(
           query.SortDirection?.Trim(),
           "desc",
           StringComparison.OrdinalIgnoreCase);

       itemsQuery = (query.SortBy?.Trim().ToLowerInvariant(), descending) switch
       {
           ("name", false) => itemsQuery
               .OrderBy(i => i.Name)
               .ThenBy(i => i.Id),
           ("name", true) => itemsQuery
               .OrderByDescending(i => i.Name)
               .ThenBy(i => i.Id),
           ("price", false) => itemsQuery
               .OrderBy(i => i.Price)
               .ThenBy(i => i.Name)
               .ThenBy(i => i.Id),
           ("price", true) => itemsQuery
               .OrderByDescending(i => i.Price)
               .ThenBy(i => i.Name)
               .ThenBy(i => i.Id),
           ("createdat", false) => itemsQuery
               .OrderBy(i => i.CreatedAt)
               .ThenBy(i => i.Id),
           ("createdat", true) => itemsQuery
               .OrderByDescending(i => i.CreatedAt)
               .ThenBy(i => i.Id),
           ("updatedat", false) => itemsQuery
               .OrderBy(i => i.UpdatedAt ?? i.CreatedAt)
               .ThenBy(i => i.Id),
           ("updatedat", true) => itemsQuery
               .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
               .ThenBy(i => i.Id),
           _ => itemsQuery
               .OrderBy(i => i.Category.DisplayOrder)
               .ThenBy(i => i.DisplayOrder)
               .ThenBy(i => i.Name)
               .ThenBy(i => i.Id)
       };

       var total = await itemsQuery.CountAsync(ct);
       var rows = await itemsQuery
           .Skip((page - 1) * pageSize)
           .Take(pageSize)
           .Select(i => new
           {
               Item = new MenuItemDto(
                   i.Id,
                   i.MenuCategoryId,
                   i.Category.MenuId,
                   i.Name,
                   i.Description,
                   i.Price,
                   i.Currency,
                   i.ImageFileId,
                   i.DisplayOrder,
                   i.IsAvailable,
                   i.IsActive,
                   i.CreatedAt,
                   i.UpdatedAt,
                   i.Category.Name),
               StorageKey = i.ImageFileId == null
                   ? null
                   : db.Files
                       .Where(f => f.Id == i.ImageFileId)
                       .Select(f => f.StorageKey)
                       .FirstOrDefault()
           })
           .ToListAsync(ct);

       var items = rows
           .Select(row => row.Item with
           {
               ImageUrl = string.IsNullOrWhiteSpace(row.StorageKey)
                   ? null
                   : $"/api/v1/files/{Uri.EscapeDataString(row.StorageKey)}"
           })
           .ToList();

       return new PagedResult<MenuItemDto>
       {
           Items = items,
           Page = page,
           PageSize = pageSize,
           TotalCount = total
       };
   }

   public async Task<MenuItemSummaryDto> GetMenuSummaryAsync(Guid menuId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemView, ct);
       await LoadOwnedMenuAsync(menuId, tenantId, ct);

       var counts = await db.MenuItems.AsNoTracking()
           .Where(i => i.Category.MenuId == menuId)
           .GroupBy(_ => 1)
           .Select(group => new
           {
               Total = group.Count(),
               Available = group.Count(i => i.IsAvailable)
           })
           .FirstOrDefaultAsync(ct);

       var categoryCount = await db.MenuCategories.AsNoTracking()
           .CountAsync(c => c.MenuId == menuId, ct);

       var total = counts?.Total ?? 0;
       var available = counts?.Available ?? 0;

       return new MenuItemSummaryDto(
           total,
           available,
           total - available,
           categoryCount);
   }

   public async Task<MenuItemDto> GetByIdAsync(Guid itemId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemView, ct);
       var item = await LoadOwnedItemAsync(itemId, tenantId, ct, track: false);
       return MenuMapping.ToDto(item, item.Category.MenuId, item.Category.Name);
   }

   public async Task<MenuItemDto> UpdateAsync(Guid itemId, UpdateMenuItemRequest request, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemUpdate, ct);
       var item = await LoadOwnedItemAsync(itemId, tenantId, ct);
       item.Category.Menu.EnsureEditable();

       Money? price = null;
       if (request.Price.HasValue)
       {
           var currency = string.IsNullOrWhiteSpace(request.Currency) ? item.Currency : request.Currency;
           price = Money.New(request.Price.Value, currency);
       }

       item.Update(request.Name, request.Description, price, displayOrder: request.DisplayOrder);

       if (request.ClearImage)
           item.SetImage(null);
       else if (request.ImageFileId.HasValue)
       {
           await EnsureUsableImageAsync(request.ImageFileId.Value, tenantId, ct);
           item.SetImage(request.ImageFileId.Value);
       }

       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(item, item.Category.MenuId, item.Category.Name);
   }

   public async Task<MenuItemDto> SetAvailabilityAsync(Guid itemId, bool isAvailable, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemUpdate, ct);
       var item = await LoadOwnedItemAsync(itemId, tenantId, ct);
       item.SetAvailability(isAvailable);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(item, item.Category.MenuId, item.Category.Name);
   }

   public async Task<MenuItemDto> SetActiveAsync(Guid itemId, bool isActive, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemUpdate, ct);
       var item = await LoadOwnedItemAsync(itemId, tenantId, ct);
       item.Category.Menu.EnsureEditable();
       item.SetActive(isActive);
       await db.SaveChangesAsync(ct);
       return MenuMapping.ToDto(item, item.Category.MenuId, item.Category.Name);
   }

   public async Task SoftDeleteAsync(Guid itemId, CancellationToken ct = default)
   {
       var tenantId = await accessGuard.EnsurePermissionAsync(Permissions.MenuItemDelete, ct);
       var item = await LoadOwnedItemAsync(itemId, tenantId, ct);
       item.Category.Menu.EnsureEditable();
       item.SoftDelete();
       await db.SaveChangesAsync(ct);
   }

   private async Task EnsureUsableImageAsync(Guid imageFileId, Guid tenantId, CancellationToken ct)
   {
       var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == imageFileId, ct)
           ?? throw new DomainException("ITEM_IMAGE_NOT_FOUND", "The referenced image file does not exist.");

       if (file.DeletedAt is not null)
           throw new DomainException("ITEM_IMAGE_DELETED", "The referenced image file is no longer available.");

       if (file.TenantId is not null && file.TenantId != tenantId)
           throw new DomainException("ITEM_IMAGE_FORBIDDEN", "The referenced image file does not belong to this tenant.");

       if (!MenuImageRules.AllowedContentTypes.Contains(file.ContentType))
           throw new DomainException("ITEM_IMAGE_TYPE", "Menu item images must be JPEG, PNG, WebP, or GIF.");

       if (file.FileSize <= 0 || file.FileSize > MenuImageRules.MaxFileSizeBytes)
           throw new DomainException("ITEM_IMAGE_SIZE", "Menu item images must be between 1 byte and 5 MB.");
   }

   private async Task<int> NextDisplayOrderAsync(Guid categoryId, CancellationToken ct)
   {
       var max = await db.MenuItems
           .Where(i => i.MenuCategoryId == categoryId)
           .Select(i => (int?)i.DisplayOrder)
           .MaxAsync(ct);
       return (max ?? -1) + 1;
   }

   private async Task<Menu> LoadOwnedMenuAsync(Guid menuId, Guid tenantId, CancellationToken ct)
   {
       var menu = await db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuId, ct)
           ?? throw new NotFoundException("Menu", menuId);

       if (menu.TenantId != tenantId)
           throw new NotFoundException("Menu", menuId);

       return menu;
   }

   private async Task<MenuCategory> LoadOwnedCategoryAsync(Guid categoryId, Guid tenantId, CancellationToken ct, bool track = true)
   {
       IQueryable<MenuCategory> query = db.MenuCategories.Include(c => c.Menu);
       if (!track)
           query = query.AsNoTracking();

       var category = await query.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
           ?? throw new NotFoundException("MenuCategory", categoryId);

       if (category.Menu.TenantId != tenantId)
           throw new NotFoundException("MenuCategory", categoryId);

       return category;
   }

   private async Task<MenuItem> LoadOwnedItemAsync(Guid itemId, Guid tenantId, CancellationToken ct, bool track = true)
   {
       IQueryable<MenuItem> query = db.MenuItems
           .Include(i => i.Category)
           .ThenInclude(c => c.Menu);

       if (!track)
           query = query.AsNoTracking();

       var item = await query.FirstOrDefaultAsync(i => i.Id == itemId, ct)
           ?? throw new NotFoundException("MenuItem", itemId);

       if (item.Category.Menu.TenantId != tenantId)
           throw new NotFoundException("MenuItem", itemId);

       return item;
   }
}
