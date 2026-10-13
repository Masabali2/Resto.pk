using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
namespace RestaurantSaaS.Infrastructure.Tenancy;

/// <summary>
/// Resolves the tenant for AUTHENTICATED dashboard requests.
///
/// SECURITY CONTRACT:
///  - The requested tenant id (e.g. from an "X-Tenant-Id" header set by the
///    client's active-workspace switcher) is only a CANDIDATE.
///  - It is accepted ONLY if the authenticated user holds an ACTIVE
///    StaffMembership in that tenant (live DB check). Otherwise => null
///    (fail closed, middleware returns 403 for tenant-scoped endpoints).
///  - No client value ever lands in ITenantContext unchecked.
/// </summary>
public sealed class AuthenticatedTenantResolver(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUser,
    ITenantMembershipService membershipService) : ITenantResolver
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task<Guid?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return null;

        var http = httpContextAccessor.HttpContext;
        if (http is null)
            return null;

        // Candidate selection: explicit header first (multi-tenant workspace switcher)…
        if (http.Request.Headers.TryGetValue(TenantHeaderName, out var header) &&
            Guid.TryParse(header.ToString(), out var candidate))
        {
            var membership = await membershipService.GetMembershipAsync(userId, candidate, cancellationToken);
            return membership?.Status == (int)MembershipStatus.Active ? candidate : null;
        }

        // …otherwise single-membership convenience: if exactly one ACTIVE
        // membership exists, it is unambiguous and safe to adopt.
        var memberships = await membershipService.GetMembershipsForUserAsync(userId, cancellationToken);
        return memberships.Count == 1 ? memberships[0].TenantId : null;
    }
}
