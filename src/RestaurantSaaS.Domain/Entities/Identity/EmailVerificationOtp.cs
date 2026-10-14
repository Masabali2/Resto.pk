
namespace RestaurantSaaS.Domain.Entities.Identity;

public sealed class EmailVerificationOtp
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string OtpHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public DateTime? InvalidatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }

    public int FailedAttempts { get; set; }

    public User User { get; set; } = null!;

    public bool IsVerified => VerifiedAtUtc.HasValue;

    public bool IsInvalidated => InvalidatedAtUtc.HasValue;
}