using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Tenants.DTOs;

namespace RestaurantSaaS.Application.Features.Tenants;

/// <summary>
/// Tenant onboarding & management use cases. Implemented in Infrastructure
/// against EF Core (transactional tenant + owner-membership creation).
/// </summary>
public interface ITenantService
{
    /// <summary>Authorized onboarding flow only (platform admin or open-registration policy).</summary>
    Task<TenantAdminDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);

    /// <summary>Current tenant for the resolved context. Requires Tenant.View permission.</summary>
    Task<TenantDto> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Update current tenant. Requires Tenant.Update permission.</summary>
    Task<TenantDto> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken ct = default);

    /// <summary>Platform-admin lookup by id (tenant filters disabled under explicit platform scope).</summary>
    Task<TenantAdminDto> GetByIdForPlatformAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Platform-admin activation/suspension/cancellation.</summary>
    Task<TenantAdminDto> ChangeStatusForPlatformAsync(Guid tenantId, string status, CancellationToken ct = default);

    /// <summary>Platform-admin cross-tenant listing with status filter and search. PlatformAdminOnly policy at the boundary.</summary>
    Task<PagedResult<TenantAdminDto>> ListForPlatformAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

/// <summary>Permission names added for Phase 3 tenant/staff operations that require elevated checks.</summary>
public static class TenantPermissions
{
    public const string View = Permissions.TenantView;
    public const string Update = Permissions.TenantUpdate;
}
