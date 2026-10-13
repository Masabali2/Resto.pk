using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Menus;

public class MenuItem : AuditableEntity, ISoftDeletable
{
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 1000;
    public const int MaxQuantityPerLine = 999;

    private const decimal MaxPrice = 9999999999999999.99m;

    public Guid MenuCategoryId { get; private set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public decimal Price { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;

    public Guid? ImageFileId { get; private set; }
    public int DisplayOrder { get; private set; }

    public bool IsAvailable { get; private set; } = true;
    public bool IsActive { get; private set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public MenuCategory Category { get; private set; } = null!;

    private MenuItem()
    {
    }

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

        ValidateName(name);
        ValidateDescription(description);
        EnsureValidPrice(price);
        ValidateDisplayOrder(displayOrder);
        ValidateImageId(imageFileId);

        return new MenuItem
        {
            MenuCategoryId = menuCategoryId,
            Name = name.Trim(),
            Description = NormalizeDescription(description),
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

        if (name is not null)
        {
            ValidateName(name);
            Name = name.Trim();
        }

        if (description is not null)
        {
            ValidateDescription(description);
            Description = NormalizeDescription(description);
        }

        if (price is not null)
        {
            EnsureValidPrice(price);
            Price = price.Amount;
            Currency = price.Currency;
        }

        if (imageFileId.HasValue)
            SetImage(imageFileId.Value);

        if (displayOrder.HasValue)
            SetDisplayOrder(displayOrder.Value);

        if (isAvailable.HasValue)
            SetAvailability(isAvailable.Value);

        if (isActive.HasValue)
            SetActive(isActive.Value);

        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanBeOrdered =>
        !IsDeleted && IsActive && IsAvailable;

    public Money GetCurrentPrice() =>
        Money.New(Price, Currency);

    public void SetImage(Guid? imageFileId)
    {
        EnsureNotDeleted();
        ValidateImageId(imageFileId);

        if (ImageFileId == imageFileId)
            return;

        ImageFileId = imageFileId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAvailability(bool isAvailable)
    {
        EnsureNotDeleted();

        if (IsAvailable == isAvailable)
            return;

        IsAvailable = isAvailable;
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

    public void SetDisplayOrder(int displayOrder)
    {
        EnsureNotDeleted();
        ValidateDisplayOrder(displayOrder);

        if (DisplayOrder == displayOrder)
            return;

        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
        IsAvailable = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("ITEM_DELETED", "A deleted menu item cannot be modified.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("ITEM_NAME_REQUIRED", "Menu item name is required.");

        if (name.Trim().Length > MaxNameLength)
            throw new DomainException("ITEM_NAME_TOO_LONG", $"Menu item name cannot exceed {MaxNameLength} characters.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description?.Trim().Length > MaxDescriptionLength)
            throw new DomainException("ITEM_DESCRIPTION_TOO_LONG", "Menu item description cannot exceed 1000 characters.");
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static void ValidateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new DomainException("ITEM_ORDER_INVALID", "Display order cannot be negative.");
    }

    private static void ValidateImageId(Guid? imageFileId)
    {
        if (imageFileId == Guid.Empty)
            throw new DomainException("ITEM_IMAGE_INVALID", "Image file ID cannot be empty.");
    }

    private static void EnsureValidPrice(Money price)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (price.Amount < 0m || price.Amount > MaxPrice)
            throw new DomainException("ITEM_PRICE_INVALID", "Menu item price is outside the supported range.");

        if (decimal.Round(price.Amount, 2) != price.Amount)
            throw new DomainException("ITEM_PRICE_PRECISION", "Menu item price supports at most two decimal places.");

        if (string.IsNullOrWhiteSpace(price.Currency))
            throw new DomainException("ITEM_CURRENCY_REQUIRED", "Menu item currency is required.");
    }
}