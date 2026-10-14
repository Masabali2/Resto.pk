using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;
}