
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Models;
using System.Text.Json;

namespace RestaurantSaaS.Api.Middleware;

/// <summary>
/// Handles unhandled exceptions and returns consistent API responses.
/// Internal exception details are never exposed to API clients.
/// </summary>
public sealed class HandleExceptionMiddleware(
    RequestDelegate next,
    ILogger<HandleExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected or cancelled the request.
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception. TraceId: {TraceId}",
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var (statusCode, errorCode, message) =
                MapException(exception);

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object>
            {
                Success = false,
                ErrorCode = errorCode,
                Message = message,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, JsonOptions));
        }
    }

    private static (
     int StatusCode,
     string ErrorCode,
     string Message) MapException(Exception exception)
    {
        return exception switch
        {
            NotAuthenticatedException ex => (
                StatusCodes.Status401Unauthorized,
                "AUTH_UNAUTHORIZED",
                ex.Message),

            CrossTenantAccessException => (
                StatusCodes.Status403Forbidden,
                "CROSS_TENANT_ACCESS_DENIED",
                "The requested resource is not accessible."),

            ForbiddenException ex => (
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                ex.Message),

            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                ex.Message),

            ConflictException ex => (
                StatusCodes.Status409Conflict,
                "CONFLICT",
                ex.Message),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "The resource was modified by another operation. Please reload and try again."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred. Please try again later.")
        };
    }
}
