namespace RestaurantSaaS.Application.Features.Platform.DTOs;

public sealed record PlatformDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int SuspendedTenants,
    int CancelledTenants,
    int PendingTenants,
    int DeletedTenants,
    int TotalUsers,
    int TotalRestaurants,
    int TotalStaffMemberships,
    int TotalSubscriptions,
    IReadOnlyDictionary<string, int> SubscriptionsByStatus);

public sealed record PlatformUserDto(
    Guid Id,
    string? UserName,
    string? Email,
    string? FirstName,
    string? LastName,
    bool EmailConfirmed,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> PlatformRoles,
    IReadOnlyList<string> TenantRoles);

public sealed record PlatformTenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<Guid> RestaurantIds,
    Guid? CurrentSubscriptionId,
    string? CurrentSubscriptionPlanName,
    string? CurrentSubscriptionStatus);
