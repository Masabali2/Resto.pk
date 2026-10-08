using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// HttpContext-backed implementation of ICurrentUserService. Reads ONLY the
/// authenticated principal — never query/body values.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? UserName => User?.Identity?.Name;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    // JWT "role" claims are populated at token issuance with GLOBAL platform
    // roles only (see JwtTokenService). Tenant roles always come from
    // StaffMembership and are re-validated per request — never trusted here.
    public IReadOnlyCollection<string> PlatformRoles =>
        User is null
            ? []
            : User.FindAll(ClaimTypes.Role)
                  .Select(c => c.Value)
                  .Where(r => r == Permissions.PlatformRoles.PlatformAdmin)
                  .Distinct(StringComparer.Ordinal)
                  .ToList();
}
