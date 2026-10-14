namespace RestaurantSaaS.Application.Common.Entitlements;

public enum EntitlementResource
{
    StaffMembers,
    MenuItems
}

public interface IEntitlementService
{
    Task EnsureCanAddAsync(Guid tenantId, EntitlementResource resource, CancellationToken ct = default);
}
