namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the authenticated principal. Implemented in Infrastructure
/// (HttpContext-based) so the Application layer never touches ASP.NET types.
/// Values here are NEVER trusted for tenant authorization on their own —
/// tenant membership is always re-validated against StaffMemberships.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Identity user id (sub claim). Null when unauthenticated.</summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    string? UserName { get; }

    string? Email { get; }

    /// <summary>Global PLATFORM roles only (e.g. PlatformAdmin). Tenant roles come from StaffMembership.</summary>
    IReadOnlyCollection<string> PlatformRoles { get; }
}
