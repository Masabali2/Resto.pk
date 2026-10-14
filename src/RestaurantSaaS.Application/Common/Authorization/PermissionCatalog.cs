
namespace RestaurantSaaS.Application.Common.Authorization;

public static class PermissionCatalog
{
    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(
            Permissions.RolePermissions.Values
                .SelectMany(permissions => permissions)
                .Concat(Permissions.AllPlatformPermissions),
            StringComparer.Ordinal);
}