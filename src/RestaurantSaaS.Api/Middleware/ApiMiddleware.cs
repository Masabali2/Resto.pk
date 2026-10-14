
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Api.Middleware;

/// <summary>
/// Resolves the tenant context before tenant-scoped authorization.
/// Each resolver must enforce the validation rules for its access model.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var setter = context.RequestServices
            .GetRequiredService<ITenantContextSetter>();

        var resolvers = context.RequestServices
            .GetServices<ITenantResolver>();

        foreach (var resolver in resolvers)
        {
            var tenantId = await resolver.ResolveAsync(
                context.RequestAborted);

            if (tenantId is null)
            {
                continue;
            }

            setter.SetTenant(tenantId.Value);
            break;
        }

        await next(context);
    }
}

