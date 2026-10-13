namespace RestaurantSaaS.Application.Common.Options;

/// <summary>Central registry of authorization policy names (no magic strings in attributes).</summary>
public static class IdentityPolicyNames
{
    public const string PlatformAdminOnly = "PlatformAdminOnly";
    public const string TenantMember = "TenantMember";
    public const string TenantOwner = "TenantOwner";
    public const string PermissionPrefix = "Permission."; // e.g. Permission.Menu.View

    public static string ForPermission(string permission) => PermissionPrefix + permission;
}
