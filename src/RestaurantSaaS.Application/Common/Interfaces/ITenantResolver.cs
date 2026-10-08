namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Resolves the effective tenant for a request. Kept out of controllers
/// (requirement #9). Two planned implementations:
///   AuthenticatedTenantResolver  – user + active StaffMembership (membership re-checked per request, not blindly trusted from JWT)
///   PublicTenantResolver         – restaurant slug / website slug / subdomain for customer traffic
/// The first resolver that returns a tenant wins; ambiguity => fail closed.
/// </summary>
public interface ITenantResolver
{
    Task<Guid?> ResolveAsync(CancellationToken cancellationToken = default);
}
