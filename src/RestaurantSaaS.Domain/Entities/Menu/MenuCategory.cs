using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Menus;

/// <summary>
/// Category inside a menu. NO TenantId column (requirement #38): tenancy is
/// derived MenuCategory -> Menu -> Tenant. Ordering is via DisplayOrder.
/// </summary>
public class MenuCategory : AuditableEntity
{
    public Guid MenuId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>UI sort order within the menu (not a guarantee of stable ranking).</summary>
    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public Menu Menu { get; private set; } = null!;
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();

    public static MenuCategory Create(Guid menuId, string name, int displayOrder = 0, string? description = null)
    {
        if (menuId == Guid.Empty)
            throw new DomainException("CATEGORY_MENU_REQUIRED", "A category must belong to a menu.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("CATEGORY_NAME_REQUIRED", "Category name is required.");

        if (name.Length > 100)
            throw new DomainException("CATEGORY_NAME_TOO_LONG", "Category name exceeds 100 characters.");

        return new MenuCategory
        {
            MenuId = menuId,
            Name = name.Trim(),
            Description = description?.Trim(),
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? name, string? description, int? displayOrder, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Length > 100)
                throw new DomainException("CATEGORY_NAME_TOO_LONG", "Category name exceeds 100 characters.");
            Name = name.Trim();
        }

        if (description is not null)
            Description = description.Trim();

        if (displayOrder.HasValue)
            DisplayOrder = displayOrder.Value;

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }
}
