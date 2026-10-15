using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Menus;

public class Menu : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;
    public MenuStatus Status { get; private set; } = MenuStatus.Draft;

    public Restaurants.Restaurant Restaurant { get; private set; } = null!;

    public ICollection<MenuCategory> Categories { get; private set; }
        = new List<MenuCategory>();

    private Menu()
    {
    }

    public static Menu Create(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string? description = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("MENU_TENANT_REQUIRED", "A menu must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException("MENU_RESTAURANT_REQUIRED", "A menu must belong to a restaurant.");

        ValidateName(name);
        var normalizedDescription = NormalizeDescription(description);

        return new Menu
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            Name = name.Trim(),
            Description = normalizedDescription,
            IsActive = true,
            Status = MenuStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? name, string? description, bool? isActive)
    {
        EnsureNotArchived();

        if (name is not null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (description is not null)
            Description = NormalizeDescription(description);

        if (isActive.HasValue)
            SetActive(isActive.Value);

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        EnsureNotArchived();

        if (IsActive == isActive)
            return;

        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureEditable()
    {
        EnsureNotArchived();
    }

    public void Publish(int categoryCount)
    {
        EnsureNotArchived();

        if (!IsActive)
            throw new DomainException("MENU_INACTIVE", "Only an active menu can be published.");

        if (categoryCount < 1)
            throw new DomainException("MENU_EMPTY", "A menu without categories cannot be published.");

        if (Status == MenuStatus.Published)
            return;

        Status = MenuStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        EnsureNotArchived();

        if (Status != MenuStatus.Published)
            throw new DomainException("MENU_NOT_PUBLISHED", "Only a published menu can be unpublished.");

        Status = MenuStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == MenuStatus.Archived)
            return;

        Status = MenuStatus.Archived;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsPubliclyVisible =>
        IsActive && Status == MenuStatus.Published;

    private void EnsureNotArchived()
    {
        if (Status == MenuStatus.Archived)
            throw new DomainException("MENU_ARCHIVED", "An archived menu cannot be modified.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("MENU_NAME_REQUIRED", "Menu name is required.");

        if (name.Trim().Length > 150)
            throw new DomainException("MENU_NAME_TOO_LONG", "Menu name cannot exceed 150 characters.");
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var normalized = description.Trim();

        if (normalized.Length > 1000)
            throw new DomainException(
                "MENU_DESCRIPTION_TOO_LONG",
                "Menu description cannot exceed 1000 characters.");

        return normalized;
    }
}