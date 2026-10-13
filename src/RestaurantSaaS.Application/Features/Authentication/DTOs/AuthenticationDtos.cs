using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

// SECURITY (requirement #19): registration input carries NO RoleId, TenantId,
// IsPlatformAdmin or Permissions fields — those can never be client-supplied.
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record SelectTenantRequest(Guid TenantId);

public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> PlatformRoles);

public sealed record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    string RoleName);   // the user's tenant-level role in THAT tenant

public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    AuthenticatedUserDto User,
    IReadOnlyList<TenantSummaryDto> Memberships,
    TenantSummaryDto? ActiveTenant);

public static class AuthMapper
{
    public static TenantSummaryDto ToDto(this TenantMembershipInfo m) =>
        new(m.TenantId, m.TenantName, m.TenantSlug, m.RoleName);
}
