
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;

namespace RestaurantSaaS.Application.Common.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthenticatedUserDto>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);
    Task<ApiResponse<LoginResult>> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> VerifyOtpAsync(
        VerifyEmailOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> ResendOtpAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<LoginResult>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AuthResultDto>> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<string>> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

}