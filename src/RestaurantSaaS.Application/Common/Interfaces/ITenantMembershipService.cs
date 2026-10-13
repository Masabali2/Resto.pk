namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// THE authoritative source of "does this user belong to this tenant, in what
/// role?". Always backed by the current StaffMemberships table — never by JWT
/// claims (tokens can be stale after a role change; requirement #13/#10).
/// </summary>
public interface ITenantMembershipService
{
    Task<TenantMembershipInfo?> GetMembershipAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    async Task<bool> IsMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
        await GetMembershipAsync(userId, tenantId, cancellationToken) is not null;

    /// <summary>All ACTIVE memberships of a user across tenants (tenant-selection UI, login response).</summary>
    Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Immutable projection of a verified membership.</summary>
public sealed record TenantMembershipInfo(
    Guid MembershipId,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    Guid RoleId,
    string RoleName,   // Owner | Manager | Kitchen | Cashier | Waiter
    int Status);       // MembershipStatus
