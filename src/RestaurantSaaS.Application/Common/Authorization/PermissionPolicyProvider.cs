
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RestaurantSaaS.Application.Common.Authorization;

public sealed class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(
        string policyName)
    {
        var existingPolicy =
            await base.GetPolicyAsync(policyName);

        if (existingPolicy is not null)
        {
            return existingPolicy;
        }

        if (!PermissionCatalog.All.Contains(policyName))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(
                new PermissionRequirement(policyName))
            .Build();
    }
}