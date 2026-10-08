namespace RestaurantSaaS.Application.Common.Authorization;

/// <summary>
/// Stable permission constants (requirement #19). Referenced by authorization
/// policies and StaffMembership role definitions — no magic strings scattered
/// through controllers. In V1 permissions are derived from tenant roles in
/// code; the shape allows moving to a DB-backed permission store later without
/// touching call sites.
/// </summary>
public static class Permissions
{
    public const string MenuView = "Menu.View";
    public const string MenuCreate = "Menu.Create";
    public const string MenuUpdate = "Menu.Update";
    public const string MenuDelete = "Menu.Delete";

    public const string OrderView = "Order.View";
    public const string OrderCreate = "Order.Create";
    public const string OrderUpdate = "Order.Update";
    public const string OrderCancel = "Order.Cancel";

    public const string StaffView = "Staff.View";
    public const string StaffInvite = "Staff.Invite";
    public const string StaffUpdate = "Staff.Update";
    public const string StaffRemove = "Staff.Remove";

    public const string WebsiteView = "Website.View";
    public const string WebsiteUpdate = "Website.Update";
    public const string WebsitePublish = "Website.Publish";

    public const string RestaurantView = "Restaurant.View";
    public const string RestaurantUpdate = "Restaurant.Update";

    /// <summary>Tenant-level role names stored via StaffMembership.RoleId → AspNetRoles.</summary>
    public static class TenantRoles
    {
        public const string Owner = "Owner";
        public const string Manager = "Manager";
        public const string Kitchen = "Kitchen";
        public const string Cashier = "Cashier";
        public const string Waiter = "Waiter";
    }

    /// <summary>Global platform role (AspNetUserRoles), NOT tenant-scoped.</summary>
    public static class PlatformRoles
    {
        public const string PlatformAdmin = "PlatformAdmin";
    }

    /// <summary>V1 role → permission map. Single source of truth for tenant authorization.</summary>
    public static IReadOnlyDictionary<string, HashSet<string>> RolePermissions { get; } =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [TenantRoles.Owner] = All(),
            [TenantRoles.Manager] = new HashSet<string>(StringComparer.Ordinal)
            {
                MenuView, MenuCreate, MenuUpdate, MenuDelete,
                OrderView, OrderCreate, OrderUpdate, OrderCancel,
                StaffView, StaffInvite, StaffUpdate,
                WebsiteView, WebsiteUpdate, WebsitePublish,
                RestaurantView, RestaurantUpdate,
            },
            [TenantRoles.Kitchen] = new HashSet<string>(StringComparer.Ordinal)
            {
                OrderView, OrderUpdate, // preparation transitions only (validated by domain rules)
                MenuView,
            },
            [TenantRoles.Cashier] = new HashSet<string>(StringComparer.Ordinal)
            {
                OrderView, OrderCreate, OrderUpdate, OrderCancel,
                MenuView,
            },
            [TenantRoles.Waiter] = new HashSet<string>(StringComparer.Ordinal)
            {
                OrderView, OrderCreate,
                MenuView,
            },
        };

    private static HashSet<string> All() =>
        new(
            [
                MenuView, MenuCreate, MenuUpdate, MenuDelete,
                OrderView, OrderCreate, OrderUpdate, OrderCancel,
                StaffView, StaffInvite, StaffUpdate, StaffRemove,
                WebsiteView, WebsiteUpdate, WebsitePublish,
                RestaurantView, RestaurantUpdate,
            ],
            StringComparer.Ordinal);
}
