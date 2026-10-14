using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

public sealed class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}