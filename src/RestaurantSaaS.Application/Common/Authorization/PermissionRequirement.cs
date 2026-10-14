
using Microsoft.AspNetCore.Authorization;

namespace RestaurantSaaS.Application.Common.Authorization;

public sealed class PermissionRequirement(string permission)
    : IAuthorizationRequirement
{
    public string Permission { get; } =
        !string.IsNullOrWhiteSpace(permission)
            ? permission
            : throw new ArgumentException(
                "Permission cannot be empty.",
                nameof(permission));
}