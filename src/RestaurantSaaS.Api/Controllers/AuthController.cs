
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Api.Services.Authentication;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;

namespace RestaurantSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuthCookieService _authCookieService;

    public AuthController(
        IAuthService authService,
        IAuthCookieService authCookieService)
    {
        _authService = authService;
        _authCookieService = authCookieService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request,
            cancellationToken);

        return this.ToActionResult(
            result,
            StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            cancellationToken);

        if (!result.Success || result.Data is null)
        {
            var failure = ApiResponse<AuthResultDto>.Fail(
                result.ErrorCode ?? "AUTH_LOGIN_FAILED",
                result.Message ?? "Login could not be completed.",
                result.Errors);

            return this.ToActionResult(failure);
        }

        var login = result.Data;

        _authCookieService.SetAuthenticationCookies(
            login.AccessToken,
            login.AccessTokenExpiresAtUtc,
            login.RefreshToken,
            login.RefreshTokenExpiresAtUtc);

        return Ok(ApiResponse<AuthResultDto>.Ok(login.Response));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        _authCookieService.ClearAuthenticationCookies();

        return NoContent();
    }
}