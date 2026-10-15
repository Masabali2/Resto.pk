namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}