
namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IEmailVerificationService
{
    Task SendOtpAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default);

    Task ResendOtpAsync(
        string email,
        CancellationToken cancellationToken = default);
}
