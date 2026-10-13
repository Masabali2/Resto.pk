namespace RestaurantSaaS.Application.Features.Tenants.DTOs;

/// <summary>Public-safe tenant projection. NEVER includes status internals beyond what the viewer may see.</summary>
public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    DateTime CreatedAt);

/// <summary>Platform-admin view — includes lifecycle fields.</summary>
public sealed record TenantAdminDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Explicitly authorized onboarding request. There is deliberately NO TenantId,
/// RoleId or ownership field — the authenticated user becomes the initial Owner
/// inside the use case, server-side.
/// </summary>
public sealed record CreateTenantRequest(string Name, string Slug);

/// <summary>Tenant self-update. Only mutable profile fields; slug/status are not updatable here.</summary>
public sealed record UpdateTenantRequest(string Name);

/// <summary>Platform-only status change.</summary>
public sealed record UpdateTenantStatusRequest(string Status); // Active | Suspended | Cancelled
