using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Menus;

/// <summary>
/// A sellable dish/drink. Price is SERVER-CONTROLLED: checkout always reads
/// the current DB price; any client-sent price is ignored (requirements #5/#29).
/// No TenantId — derives through MenuCategory -> Menu -> Tenant.
///
/// Deletion policy: menu items are SOFT-deleted (IsDeleted) because historical
/// OrderItems reference them; orders snapshot name/price so integrity survives.
/// </summary>
public class MenuItem : AuditableEntity, ISoftDeletable
{
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 1000;

    public Guid MenuCategoryId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Stored as decimal(18,4); currency inherited from tenant configuration in V1.</summary>
    public decimal Price { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    /// <summary>Optional image (FileMetadata row).</summary>
    public Guid? ImageFileId { get; private set; }

    public int DisplayOrder { get; private set; }

    /// <summary>Kitchen availability toggle ("sold out").</summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>Admin visibility toggle (hidden from menus but retained).</summary>
    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public MenuCategory Category { get; private set; } = null!;

    public static MenuItem Create(
        Guid menuCategoryId,
        string name,
        Money price,
        string? description = null,
        Guid? imageFileId = null,
        int displayOrder = 0)
    {
        if (menuCategoryId == Guid.Empty)
            throw new DomainException("ITEM_CATEGORY_REQUIRED", "A menu item must belong to a category.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("ITEM_NAME_REQUIRED", "Menu item name is required.");

        if (name.Length > MaxNameLength)
            throw new DomainException("ITEM_NAME_TOO_LONG", $"Menu item name exceeds {MaxNameLength} characters.");

        if (description?.Length > MaxDescriptionLength)
            throw new DomainException("ITEM_DESCRIPTION_TOO_LONG", "Menu item description is too long.");

        if (price.Amount <= 0m)
            throw new DomainException("ITEM_PRICE_INVALID", "Menu item price must be greater than zero.");

        return new MenuItem
        {
            MenuCategoryId = menuCategoryId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = price.Amount,
            Currency = price.Currency,
            ImageFileId = imageFileId,
            DisplayOrder = displayOrder,
            IsAvailable = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        Money? price = null,
        Guid? imageFileId = null,
        int? displayOrder = null,
        bool? isAvailable = null,
        bool? isActive = null)
    {
        EnsureNotDeleted();

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Length > MaxNameLength)
                throw new DomainException("ITEM_NAME_TOO_LONG", $"Menu item name exceeds {MaxNameLength} characters.");
            Name = name.Trim();
        }

        if (description is not null)
        {
            if (description.Length > MaxDescriptionLength)
                throw new DomainException("ITEM_DESCRIPTION_TOO_LONG", "Menu item description is too long.");
            Description = description.Trim();
        }

        if (price is not null)
        {
            if (price.Amount <= 0m)
                throw new DomainException("ITEM_PRICE_INVALID", "Menu item price must be greater than zero.");
            Price = price.Amount;
            Currency = price.Currency;
        }

        if (imageFileId is not null)
            ImageFileId = imageFileId;

        if (displayOrder.HasValue)
            DisplayOrder = displayOrder.Value;

        if (isAvailable.HasValue)
            IsAvailable = isAvailable.Value;

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Checkout gate: only active, available, non-deleted items can be ordered.</summary>
    public bool CanBeOrdered => !IsDeleted && IsActive && IsAvailable;

    public Money GetCurrentPrice() => Money.New(Price, Currency);

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("ITEM_DELETED", "A deleted menu item cannot be modified.");
    }
}
