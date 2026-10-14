
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

// Public registration input.
// Clients cannot assign roles, permissions, or tenant membership.
public sealed record RegisterRequest(
    string Email,
    string UserName,
    string Password,
    string ConfirmPassword);

public sealed record LoginRequest(
    string EmailOrUserName,
    string Password);

public sealed record SelectTenantRequest(
    Guid TenantId);

// Safe user information returned by the API.
public sealed record AuthenticatedUserDto(
    Guid Id,
    string Email,
    string UserName,
    IReadOnlyList<string> PlatformRoles);

// A tenant membership and the user's role within that tenant.

public sealed record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    string RoleName);

public sealed record AuthResultDto(
    AuthenticatedUserDto User,
    IReadOnlyList<TenantSummaryDto> Memberships,
    TenantSummaryDto? ActiveTenant,
    IReadOnlyList<string> Permissions);

// Internal service result passed to the API layer.
// Never return this object directly as an HTTP JSON response.
public sealed record LoginResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    AuthResultDto Response);

public static class AuthMapper
{
    public static TenantSummaryDto ToDto(
        this TenantMembershipInfo membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new TenantSummaryDto(
            membership.TenantId,
            membership.TenantName,
            membership.TenantSlug,
            membership.RoleName);
    }
}