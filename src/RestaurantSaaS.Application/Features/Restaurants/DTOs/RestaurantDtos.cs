namespace RestaurantSaaS.Application.Features.Restaurants.DTOs;

public sealed record RestaurantDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    string? Phone,
    string? Email,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Country,
    string TimeZone,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Create request. NO TenantId field — the tenant comes exclusively from the
/// resolved tenant context (never from the client).
/// </summary>
public sealed record CreateRestaurantRequest(
    string Name,
    string? Description,
    string? Phone,
    string? Email,
    AddressInput Address,
    string? TimeZone);

/// <summary>Update request. NO TenantId — reassignment is impossible by design.</summary>
public sealed record UpdateRestaurantRequest(
    string? Name,
    string? Description,
    string? Phone,
    string? Email,
    AddressInput? Address,
    string? TimeZone);

public sealed record AddressInput(
    string Line1,
    string? Line2,
    string City,
    string Country,
    string? PostalCode);

/// <summary>Status name: Draft | Active | TemporarilyClosed | Closed.</summary>
public sealed record UpdateRestaurantStatusRequest(string Status);
