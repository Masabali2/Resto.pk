using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Platform.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IPlatformAdminService
{
    Task<PlatformDashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<PagedResult<PlatformUserDto>> ListUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PlatformUserDto?> GetUserAsync(Guid userId, CancellationToken ct = default);

    Task SetUserLockoutAsync(Guid userId, bool lockedOutUntilNextSignIn, CancellationToken ct = default);

    Task<PlatformTenantDetailDto> GetTenantDetailAsync(Guid tenantId, CancellationToken ct = default);
}
