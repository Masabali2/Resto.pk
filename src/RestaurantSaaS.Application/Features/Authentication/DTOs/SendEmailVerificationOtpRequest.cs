using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Features.Auth.DTOs;

public sealed class SendEmailVerificationOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}