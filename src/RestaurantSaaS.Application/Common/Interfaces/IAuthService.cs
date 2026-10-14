
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

}