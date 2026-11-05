namespace RestaurantSaaS.Application.Features.Restaurants.DTOs;

/// <summary>
/// Result of public restaurant resolution for ordering.
/// Excludes internal administrative data; only what a public storefront
/// needs to place an order.
/// </summary>
public sealed record PublicOrderingRestaurantDto(
    Guid Id,
    Guid TenantId,
    Guid WebsiteId,
    string Name,
    string Slug,
    string City,
    string Country,
    string TimeZone,
    decimal TaxRate);
