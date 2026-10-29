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
    DateTime? UpdatedAt,
    string? CategoryName = null,
    string? ImageUrl = null);

/// <summary>
/// Menu-item dashboard totals. Calculated in one aggregate query so the
/// stat cards never depend on the current page of results.
/// </summary>
public sealed record MenuItemSummaryDto(
    int TotalItems,
    int AvailableItems,
    int UnavailableItems,
    int CategoryCount);

/// <summary>
/// Server-side list contract for the menu management screen. Every filter is
/// optional; paging and sorting are whitelisted in the service layer.
/// </summary>
public sealed record MenuItemListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsAvailable { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
}

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
