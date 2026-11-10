using RestaurantSaaS.Application.Features.MenuCategories.DTOs;

using RestaurantSaaS.Application.Features.MenuItems.DTOs;
using RestaurantSaaS.Domain.Enums;

namespace RestaurantSaaS.Application.Features.Orders.DTOS;

public sealed record StaffCreateOrderDto(
    Guid RestaurantId,
    string RestaurantName,
    decimal TaxRate,
    string Currency,
    Guid MenuId,
    string MenuName,
    IReadOnlyList<MenuCategoryDto> Categories,
    IReadOnlyList<MenuItemDto> Items,
    IReadOnlyList<OrderType> OrderTypes,
    PaymentMethod PaymentMethod);