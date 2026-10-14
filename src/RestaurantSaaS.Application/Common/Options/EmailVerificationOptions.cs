
using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Common.Options;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    [Required]
    [MinLength(32)]
    public string HmacKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int ExpirationMinutes { get; set; } = 10;

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 5;
    [Range(1, 3600)]
    public int ResendCooldownSeconds { get; set; } = 60;

    [Range(1, 20)]
    public int MaxSendsPerHour { get; set; } = 3;
}
