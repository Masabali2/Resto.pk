using RestaurantSaaS.Application.Features.MenuCategories.DTOs;
using RestaurantSaaS.Application.Features.MenuItems.DTOs;

namespace RestaurantSaaS.Application.Features.Menus.DTOs;

public sealed record MenuDto(
   Guid Id,
   Guid TenantId,
   Guid RestaurantId,
   string Name,
   string? Description,
   bool IsActive,
   string Status,
   DateTime CreatedAt,
   DateTime? UpdatedAt);

public sealed record MenuDetailsDto(
   Guid Id,
   Guid TenantId,
   Guid RestaurantId,
   string Name,
   string? Description,
   bool IsActive,
   string Status,
   DateTime CreatedAt,
   DateTime? UpdatedAt,
   IReadOnlyList<MenuCategoryDto> Categories);

/// <summary>
/// Read-only projection of the platform default menu (the catalogue owned by
/// the demo tenant). It is the only menu a tenant may read outside its own
/// tenant scope, and it is never editable through this contract.
/// </summary>
public sealed record DefaultMenuDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    int CategoryCount,
    int ItemCount,
    IReadOnlyList<DefaultMenuCategoryDto> Categories);

public sealed record DefaultMenuCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    IReadOnlyList<MenuItemDto> Items);

public sealed record CreateMenuRequest(string Name, string? Description);

public sealed record UpdateMenuRequest(string? Name, string? Description);

public sealed record UpdateMenuStatusRequest(bool IsActive);

public sealed record UpdateMenuPublishRequest(bool Publish);
