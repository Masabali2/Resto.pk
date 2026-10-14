
using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Features.Authentication.DTOs;

public sealed class VerifyEmailOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Otp { get; set; } = string.Empty;
}
