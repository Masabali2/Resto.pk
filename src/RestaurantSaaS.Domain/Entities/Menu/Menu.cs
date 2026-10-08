using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Menus;

/// <summary>
/// A menu belonging to one restaurant of one tenant. A restaurant may have
/// multiple menus (lunch/dinner/seasonal).
///
/// TENANT INTEGRITY: carries BOTH TenantId and RestaurantId. The EF layer maps
/// a composite FK (TenantId, RestaurantId) -> Restaurants(TenantId, Id), so a
/// "Menu of Tenant A pointing at Restaurant of Tenant B" is rejected by the
/// database itself, not only by application code (requirement #11).
/// </summary>
public class Menu : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Soft on/off switch used by tenants; independent from lifecycle Status.</summary>
    public bool IsActive { get; private set; } = true;

    public MenuStatus Status { get; private set; } = MenuStatus.Draft;

    public Restaurant Restaurant { get; private set; } = null!;
    public ICollection<MenuCategory> Categories { get; set; } = new List<MenuCategory>();

    public static Menu Create(Guid tenantId, Guid restaurantId, string name, string? description = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("MENU_TENANT_REQUIRED", "A menu must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException("MENU_RESTAURANT_REQUIRED", "A menu must belong to a restaurant.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("MENU_NAME_REQUIRED", "Menu name is required.");

        if (name.Length > 150)
            throw new DomainException("MENU_NAME_TOO_LONG", "Menu name exceeds 150 characters.");

        return new Menu
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            Status = MenuStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? name, string? description, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Length > 150)
                throw new DomainException("MENU_NAME_TOO_LONG", "Menu name exceeds 150 characters.");
            Name = name.Trim();
        }

        if (description is not null)
            Description = description.Trim();

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Publish rule: only an active menu with at least one category can go live.</summary>
    public void Publish()
    {
        if (Status == MenuStatus.Archived)
            throw new DomainException("MENU_ARCHIVED", "An archived menu cannot be published.");

        if (!IsActive)
            throw new DomainException("MENU_INACTIVE", "Only an active menu can be published.");

        if (Categories.Count == 0)
            throw new DomainException("MENU_EMPTY", "A menu without categories cannot be published.");

        Status = MenuStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = MenuStatus.Archived;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Public websites expose only published+active menus.</summary>
    public bool IsPubliclyVisible => IsActive && Status == MenuStatus.Published;
}
