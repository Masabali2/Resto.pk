using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Associates an uploaded image with a single menu item. Replacement updates
/// only that item's <see cref="MenuItem.ImageFileId"/> and never touches
/// other items or branches.
/// </summary>
public sealed class MenuItemUploadHandler : IUploadDestinationHandler
{
    public string Kind => "menu-item";

    public async Task ApplyAsync(
        FileMetadata file,
        UploadDestination destination,
        Guid userId,
        IUploadDestinationContext context,
        CancellationToken cancellationToken)
    {
        if (destination is not MenuItemDestination d)
            throw new DomainException("UPLOAD_INVALID_DESTINATION",
                "Expected a menu-item destination.");

        if (d.MenuItemId == Guid.Empty)
            throw new DomainException("UPLOAD_MENU_ITEM_REQUIRED", "A menu item is required.");

        var item = await context.FindMenuItemAsync(d.MenuItemId, cancellationToken);
        if (item is null)
            throw new DomainException("UPLOAD_MENU_ITEM_NOT_FOUND",
                "The menu item could not be found or is not accessible.");

        if (item.Category is null || item.Category.Menu is null ||
            item.Category.Menu.TenantId != context.TenantId)
            throw new DomainException("UPLOAD_MENU_ITEM_CROSS_TENANT",
                "The menu item does not belong to the current tenant.");

        var isMember = await context.IsActiveMemberAsync(userId, cancellationToken);
        if (!isMember)
            throw new DomainException("UPLOAD_FORBIDDEN",
                "You are not authorized to modify this menu item.");

        item.SetImage(file.Id);
    }
}
