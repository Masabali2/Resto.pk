namespace RestaurantSaaS.Application.Features.MenuCategories.DTOs;

public sealed record MenuCategoryDto(
    Guid Id,
    Guid MenuId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateMenuCategoryRequest(string Name, string? Description, int? DisplayOrder);

public sealed record UpdateMenuCategoryRequest(string? Name, string? Description, int? DisplayOrder);

public sealed record UpdateMenuCategoryStatusRequest(bool IsActive);

public sealed record ReorderMenuCategoriesRequest(IReadOnlyList<Guid> OrderedCategoryIds);
