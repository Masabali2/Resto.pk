using Microsoft.AspNetCore.Authorization;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Authorization;

namespace RestaurantSaaS.Api.Extensions;

public static class AuthorizationSetup
{
    public static IServiceCollection AddTenantAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityPolicyNames.PlatformAdminOnly, p =>
                p.RequireAssertion(ctx => ctx.User.IsInRole(Permissions.PlatformRoles.PlatformAdmin))
                 .AddRequirements(new PlatformAdminOnlyRequirement()));

            options.AddPolicy(IdentityPolicyNames.TenantMember, p =>
                p.AddRequirements(new TenantMemberRequirement()));

            options.AddPolicy(IdentityPolicyNames.TenantOwner, p =>
                p.AddRequirements(new TenantOwnerRequirement()));

            foreach (var permission in AllPermissions())
            {
                options.AddPolicy(
                    IdentityPolicyNames.ForPermission(permission),
                    p => p.AddRequirements(new TenantPermissionRequirement(permission)));
            }
        });

        services.AddScoped<IAuthorizationHandler, TenantPermissionHandler>();
        services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();
        services.AddScoped<IAuthorizationHandler, TenantOwnerHandler>();
        services.AddScoped<IAuthorizationHandler, PlatformAdminOnlyHandler>();
        return services;
    }

    private static IEnumerable<string> AllPermissions() =>
        typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct();
}
