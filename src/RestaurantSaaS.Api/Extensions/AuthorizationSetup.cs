using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Api.Extensions;

/// <summary>
/// Authorization wiring (requirement #10): dynamic permission policies + named
/// static policies. Controllers reference policy NAMES only — the tenant-role →
/// permission mapping lives in exactly one place (Permissions.RolePermissions).
/// </summary>
public static class AuthorizationSetup
{
    public static IServiceCollection AddTenantAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Platform authority: GLOBAL Identity role membership, validated live.
            .AddPolicy(IdentityPolicyNames.PlatformAdminOnly,
                p => p.RequireAssertion(ctx => ctx.User.IsInRole(Permissions.PlatformRoles.PlatformAdmin))
                      .AddRequirements(new Infrastructure.Authorization.PlatformAdminOnlyRequirement()))

            // Tenant membership (any active role).
            .AddPolicy(IdentityPolicyNames.TenantMember,
                p => p.AddRequirements(new Infrastructure.Authorization.TenantMemberRequirement()))

            // Tenant owner specifically.
            .AddPolicy(IdentityPolicyNames.TenantOwner,
                p => p.AddRequirements(new Infrastructure.Authorization.TenantOwnerRequirement()));

        // One "Permission.<name>" policy per capability so attributes stay declarative.
        foreach (var permission in AllPermissions())
        {
            services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
                sp.GetRequiredService<Microsoft.AspNetCore.Authorization.AuthorizationPolicyProvider>());

            services.AddAuthorizationOptions().NamedActions.Clear(); // no-op guard placeholder

            services.Configure<AuthorizationOptions>(options =>
            {
                options.AddPolicy(IdentityPolicyNames.ForPermission(permission),
                    p => p.AddRequirements(new Infrastructure.Authorization.TenantPermissionRequirement(permission)));
            });
        }

        services.AddScoped<IAuthorizationHandler, Infrastructure.Authorization.TenantAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, Infrastructure.Authorization.PlatformAdminOnlyHandler>();

        return services;
    }

    private static IEnumerable<string> AllPermissions() =>
        typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct();
}
