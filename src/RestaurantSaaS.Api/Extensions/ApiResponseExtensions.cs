
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Application.Common.Models;

namespace RestaurantSaaS.Api.Extensions;

public static class ApiResponseExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        ApiResponse<T> response,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (response.Success)
        {
            return controller.StatusCode(successStatusCode, response);
        }

        return response.ErrorCode switch
        {
            "AUTH_EMAIL_ALREADY_EXISTS" =>
                controller.Conflict(response),

            "AUTH_INVALID_REFRESH_TOKEN" or
            "AUTH_REFRESH_REUSED" or
            "AUTH_UNAUTHORIZED" =>
                controller.Unauthorized(response),

            "AUTH_FORBIDDEN" =>
                controller.StatusCode(
                    StatusCodes.Status403Forbidden, response),

            "AUTH_INVALID_REGISTRATION" or
            "AUTH_REGISTRATION_FAILED" =>
                controller.BadRequest(response),

            _ =>
                controller.BadRequest(response)
        };
    }
}