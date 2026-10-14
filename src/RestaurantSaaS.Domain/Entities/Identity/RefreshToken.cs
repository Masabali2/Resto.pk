
namespace RestaurantSaaS.Domain.Entities.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // SHA-256 hash only. Never store the raw refresh token.
    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    // Null means the token has not been revoked.
    public DateTime? RevokedAtUtc { get; set; }

    // Hash of the replacement token after rotation.
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public string? RevokedByIp { get; set; }

    public User User { get; set; } = null!;
}

