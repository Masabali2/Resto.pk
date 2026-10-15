namespace RestaurantSaaS.Application.Features.Restaurants.DTOs;

public sealed record RestaurantAssetDto(
    Guid Id,
    string AssetType,
    Guid FileId,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Url);

public sealed record PublicRestaurantAssetDto(
    string AssetType,
    string Url,
    string ContentType);
