namespace RestaurantSaaS.Domain.Enums;

/// <summary>
/// Menu lifecycle. Only <see cref="Published"/> menus are visible on public
/// websites, and only when the menu is active and its restaurant is active.
/// </summary>
public enum MenuStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}
