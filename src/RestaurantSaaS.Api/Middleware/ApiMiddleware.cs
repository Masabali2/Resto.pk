using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;

namespace RestaurantSaaS.Api.Middleware;

/// <summary>
/// Resolves the request's tenant BEFORE authorization runs. Order matters:
///   1. Authenticated resolvers first (membership-verified).
///   2. Public resolvers next (slug/subdomain for anonymous customer traffic).
/// A client-supplied TenantId NEVER enters ITenantContext without passing
/// through a resolver that validates membership server-side (fail closed).
/// </summary>
public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    IEnumerable<ITenantResolver> resolvers)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var setter = context.RequestServices.GetRequiredService<ITenantContextSetter>();

        foreach (var resolver in resolvers)
        {
            var tenantId = await resolver.ResolveAsync(context.RequestAborted);
            if (tenantId is not null)
            {
                setter.SetTenant(tenantId.Value);
                break; // first resolution wins; ambiguity impossible by construction
            }
        }

        await next(context);
    }
}

/// <summary>
/// Central exception → HTTP mapping so controllers stay thin and error payloads
/// are uniform (ApiResponse envelope). Never leaks stack traces or other
/// tenants' data.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (RestaurantSaaS.Domain.Exceptions.DomainException ex)
        {
            Write(context, StatusCodes.Status400BadRequest, ex.Code, ex.Message);
        }
        catch (Application.Common.Exceptions.NotAuthenticatedException ex)
        {
            Write(context, StatusCodes.Status401Unauthorized, ex.Code, ex.Message);
        }
        catch (Application.Common.Exceptions.CrossTenantAccessException ex)
        {
            // Generic message — never reveal whether the resource exists elsewhere.
            Write(context, StatusCodes.Status403Forbidden, "ACCESS_DENIED", ex.Message);
        }
        catch (Application.Common.Exceptions.ForbiddenException ex)
        {
            Write(context, StatusCodes.Status403Forbidden, "FORBIDDEN", ex.Message);
        }
        catch (Application.Common.Exceptions.NotFoundException ex)
        {
            Write(context, StatusCodes.Status404NotFound, "NOT_FOUND", ex.Message);
        }
        catch (Application.Common.Exceptions.ConflictException ex)
        {
            Write(context, StatusCodes.Status409Conflict, "CONFLICT", ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            Write(context, StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT",
                "The resource was modified concurrently. Reload and retry.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            Write(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static void Write(HttpContext context, int status, string errorCode, string message)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        // ApiResponse<T> is a class with init-only props; construct directly.
        var payload = new ApiResponse<object>
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions)).GetAwaiter().GetResult();
    }
}
