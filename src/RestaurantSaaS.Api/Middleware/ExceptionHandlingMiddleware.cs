
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

            var (statusCode, errorCode, message, retryAfter) =
                MapException(exception);

            if (retryAfter is not null)
            {
                context.Response.Headers.RetryAfter =
                    retryAfter.Value.ToString();
            }

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
     string Message,
     int? RetryAfterSeconds) MapException(Exception exception)
    {
        return exception switch
        {
            OtpRateLimitException ex => (
                StatusCodes.Status429TooManyRequests,
                "OTP_RATE_LIMITED",
                ex.Message,
                ex.RetryAfterSeconds),

            NotAuthenticatedException ex => (
                StatusCodes.Status401Unauthorized,
                "AUTH_UNAUTHORIZED",
                ex.Message,
                null),

            CrossTenantAccessException => (
                StatusCodes.Status403Forbidden,
                "CROSS_TENANT_ACCESS_DENIED",
                "The requested resource is not accessible.",
                null),

            ForbiddenException ex => (
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                ex.Message,
                null),

            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                ex.Message,
                null),

            ConflictException ex => (
                StatusCodes.Status409Conflict,
                "CONFLICT",
                ex.Message,
                null),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "The resource was modified by another operation. Please reload and try again.",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred. Please try again later.",
                null)
        };
    }
}
