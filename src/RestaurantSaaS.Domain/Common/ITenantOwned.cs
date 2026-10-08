namespace RestaurantSaaS.Domain.Common;

/// <summary>
/// Marker for entities that carry a direct <c>TenantId</c> column and are
/// therefore subject to the EF Core tenant global query filter and explicit
/// ownership validation.
///
/// DESIGN NOTE (requirement #38): Not every entity implements this.
/// Dependent entities (MenuCategory, MenuItem, OrderItem, WebsiteConfiguration…)
/// derive tenancy through their parent aggregate and deliberately do NOT
/// duplicate TenantId. Tenant integrity for those rows is enforced by
/// composite foreign keys (TenantId, ParentId) at the database level.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
