namespace RestaurantSaaS.Application.Features.MenuItems.DTOs;

public sealed record MenuItemDto(
    Guid Id,
    Guid MenuCategoryId,
    Guid MenuId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid? ImageFileId,
    int DisplayOrder,
    bool IsAvailable,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateMenuItemRequest(
    string Name,
    decimal Price,
    string? Description,
    string? Currency,
    Guid? ImageFileId,
    int? DisplayOrder);

public sealed record UpdateMenuItemRequest(
    string? Name,
    string? Description,
    decimal? Price,
    string? Currency,
    Guid? ImageFileId,
    bool ClearImage,
    int? DisplayOrder);

public sealed record UpdateMenuItemAvailabilityRequest(bool IsAvailable);

public sealed record UpdateMenuItemStatusRequest(bool IsActive);
