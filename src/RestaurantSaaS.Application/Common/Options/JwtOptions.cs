using System.ComponentModel.DataAnnotations;

namespace RestaurantSaaS.Application.Common.Options;

/// <summary>
/// Strongly-typed JWT configuration (section "Jwt"). Bound via IOptions with
/// validation. Secret comes from environment/user-secrets in development and a
/// secure secret store in production — it is NEVER committed to source control.
/// </summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = null!;
    [Required] public string Audience { get; init; } = null!;
    [Required] public string Secret { get; init; } = null!;
    [Range(1, 60)] public int AccessTokenExpirationMinutes { get; init; } = 30;
    [Range(1, 90)] public int RefreshTokenExpirationDays { get; init; } = 7;


    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(Secret))
        {
            yield return new ValidationResult(
                "Jwt:Secret is required.");
            yield break;
        }

        if (Secret.Length < 32)
        {
            yield return new ValidationResult(
                "Jwt:Secret must be at least 32 characters.");
        }
    }

}
