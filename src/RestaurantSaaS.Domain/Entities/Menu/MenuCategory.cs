
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Menus;

public class MenuCategory : AuditableEntity, ISoftDeletable
{
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 1000;

    public Guid MenuId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Menu Menu { get; private set; } = null!;

    private readonly List<MenuItem> _items = [];

    public IReadOnlyCollection<MenuItem> Items => _items.AsReadOnly();

    private MenuCategory()
    {
    }

    public static MenuCategory Create(
        Guid menuId,
        string name,
        string? description = null,
        int displayOrder = 0)
    {
        if (menuId == Guid.Empty)
            throw new DomainException(
                "CATEGORY_MENU_REQUIRED",
                "A menu category must belong to a menu.");

        ValidateName(name);
        ValidateDescription(description);
        ValidateDisplayOrder(displayOrder);

        return new MenuCategory
        {
            MenuId = menuId,
            Name = name.Trim(),
            Description = NormalizeDescription(description),
            DisplayOrder = displayOrder,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        bool updateDescription = false)
    {
        EnsureNotDeleted();

        if (name is not null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (updateDescription || description is not null)
        {
            ValidateDescription(description);
            Description = NormalizeDescription(description);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDisplayOrder(int displayOrder)
    {
        EnsureNotDeleted();
        ValidateDisplayOrder(displayOrder);

        if (DisplayOrder == displayOrder)
            return;

        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        EnsureNotDeleted();

        if (IsActive == isActive)
            return;

        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsVisible =>
        !IsDeleted && IsActive;

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException(
                "CATEGORY_DELETED",
                "A deleted menu category cannot be modified.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(
                "CATEGORY_NAME_REQUIRED",
                "Menu category name is required.");

        if (name.Trim().Length > MaxNameLength)
            throw new DomainException(
                "CATEGORY_NAME_TOO_LONG",
                $"Menu category name cannot exceed {MaxNameLength} characters.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description?.Trim().Length > MaxDescriptionLength)
            throw new DomainException(
                "CATEGORY_DESCRIPTION_TOO_LONG",
                $"Menu category description cannot exceed {MaxDescriptionLength} characters.");
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static void ValidateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new DomainException(
                "CATEGORY_ORDER_INVALID",
                "Display order cannot be negative.");
    }
}
