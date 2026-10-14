using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Api.Services.Authentication;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;

namespace RestaurantSaaS.Api.Tests;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IAuthCookieService> _cookieService = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(
            _authService.Object,
            _cookieService.Object);
    }

    [Fact]
    public async Task Refresh_WhenCookieMissing_Returns401WithoutCallingService()
    {
        _cookieService
            .Setup(x => x.TryReadRefreshToken(out It.Ref<string>.IsAny))
            .Returns(false);

        var result = await _controller.Refresh(CancellationToken.None);

        (result as ObjectResult)!.StatusCode.Should().Be(401);
        _authService.Verify(
            x => x.RefreshAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Refresh_WhenRotationSucceeds_SetsCookiesAndHidesRawToken()
    {
        var refreshToken = "opaque-refresh-token";
        var expires = DateTime.UtcNow.AddDays(7);

        _cookieService
            .Setup(x => x.TryReadRefreshToken(out It.Ref<string>.IsAny))
            .Returns((out string t) =>
            {
                t = refreshToken;
                return true;
            });

        var userDto = new AuthenticatedUserDto(
            Guid.NewGuid(),
            "user@example.com",
            "user",
            Array.Empty<string>());

        var authResult = new AuthResultDto(
            userDto,
            Array.Empty<TenantSummaryDto>(),
            null,
            Array.Empty<string>());

        var loginResult = new LoginResult(
            "jwt-access",
            expires,
            refreshToken + "-new",
            expires,
            authResult);

        _authService
            .Setup(x => x.RefreshAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<LoginResult>.Ok(loginResult));

        var result = await _controller.Refresh(CancellationToken.None);

        var ok = result as ObjectResult;
        ok.Should().NotBeNull();

        var envelope = ok!.Value as ApiResponse<AuthResultDto>;
        envelope!.Success.Should().BeTrue();
        envelope.Data!.User.Email.Should().Be("user@example.com");

        ok.Value.Should().NotBeSameAs(loginResult);

        _cookieService.Verify(
            x => x.SetAuthenticationCookies(
                "jwt-access",
                It.IsAny<DateTime>(),
                refreshToken + "-new",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenRotationFails_ClearsCookiesAndReturns401()
    {
        var refreshToken = "rotated-token";

        _cookieService
            .Setup(x => x.TryReadRefreshToken(out It.Ref<string>.IsAny))
            .Returns((out string t) =>
            {
                t = refreshToken;
                return true;
            });

        _authService
            .Setup(x => x.RefreshAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<LoginResult>.Fail(
                "AUTH_REFRESH_REUSED",
                "The refresh token has already been used."));

        var result = await _controller.Refresh(CancellationToken.None);

        (result as ObjectResult)!.StatusCode.Should().Be(401);
        _cookieService.Verify(x => x.ClearAuthenticationCookies(), Times.Once);
    }

    [Fact]
    public async Task Logout_RevokesCurrentSessionTokenAndClearsCookies()
    {
        var refreshToken = "session-token";

        _cookieService
            .Setup(x => x.TryReadRefreshToken(out It.Ref<string>.IsAny))
            .Returns((out string t) =>
            {
                t = refreshToken;
                return true;
            });

        _authService
            .Setup(x => x.LogoutAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<string>.Ok("Signed out."));

        var result = await _controller.Logout(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _authService.Verify(
            x => x.LogoutAsync(refreshToken, It.IsAny<CancellationToken>()),
            Times.Once);
        _cookieService.Verify(x => x.ClearAuthenticationCookies(), Times.Once);
    }

    [Fact]
    public async Task Me_WhenServiceReturnsProfile_Returns200WithSafeDto()
    {
        var userDto = new AuthenticatedUserDto(
            Guid.NewGuid(),
            "user@example.com",
            "user",
            new[] { "PlatformAdmin" });

        var authResult = new AuthResultDto(
            userDto,
            Array.Empty<TenantSummaryDto>(),
            null,
            new[] { "platform.admin" });

        _authService
            .Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<AuthResultDto>.Ok(authResult));

        var result = await _controller.Me(CancellationToken.None);

        var created = result as ObjectResult;
        created!.StatusCode.Should().Be(200);
        var envelope = created.Value as ApiResponse<AuthResultDto>;
        envelope!.Data!.User.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task Me_WhenIdentityInvalid_Returns401()
    {
        _authService
            .Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<AuthResultDto>.Fail(
                "AUTH_UNAUTHORIZED",
                "The request does not carry a valid authenticated identity."));

        var result = await _controller.Me(CancellationToken.None);

        (result as ObjectResult)!.StatusCode.Should().Be(401);
    }

    [Fact]
    public void ToActionResult_MapsRefreshErrorCodesToUnauthorized()
    {
        var controller = new TestableController();

        var response = ApiResponse<object>.Fail(
            "AUTH_INVALID_REFRESH_TOKEN",
            "invalid");

        var result = controller.Invoke(response);

        (result as ObjectResult)!.StatusCode.Should().Be(401);
    }

    private sealed class TestableController : ControllerBase
    {
        public IActionResult Invoke<T>(ApiResponse<T> response) =>
            this.ToActionResult(response);
    }
}
