
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Authentication.DTOs;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IDateTimeProvider dateTimeProvider,
    IJwtTokenService jwtTokenService,
    ITenantMembershipService tenantMembershipService,
    IEmailVerificationService emailVerificationService,
    ApplicationDbContext dbContext,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<ApiResponse<AuthenticatedUserDto>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = request.Email?.Trim() ?? string.Empty;
        var userName = request.UserName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "AUTH_INVALID_REGISTRATION",
                "Email, username, password, and password confirmation are required.");
        }

        if (!string.Equals(
            request.Password,
            request.ConfirmPassword,
            StringComparison.Ordinal))
        {
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "AUTH_INVALID_REGISTRATION",
                "Password and confirmation password do not match.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "AUTH_EMAIL_ALREADY_EXISTS",
                "An account with this email already exists.");
        }

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return ApiResponse<AuthenticatedUserDto>.Fail(
                "AUTH_USERNAME_ALREADY_EXISTS",
                "This username is already taken.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            CreatedAt = dateTimeProvider.UtcNow,
            EmailConfirmed = false
        };

        var createResult = await userManager.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            var duplicateEmail = createResult.Errors.Any(error =>
                error.Code.Contains(
                    "DuplicateEmail",
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateEmail)
            {
                return ApiResponse<AuthenticatedUserDto>.Fail(
                    "AUTH_EMAIL_ALREADY_EXISTS",
                    "An account with this email already exists.");
            }

            var duplicateUserName = createResult.Errors.Any(error =>
                error.Code.Contains(
                    "DuplicateUserName",
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateUserName)
            {
                return ApiResponse<AuthenticatedUserDto>.Fail(
                    "AUTH_USERNAME_ALREADY_EXISTS",
                    "This username is already taken.");
            }

            var errors = createResult.Errors
                .Select(error => new ValidationError(
                    "Registration",
                    error.Description))
                .ToList();

            return ApiResponse<AuthenticatedUserDto>.Fail(
                "AUTH_REGISTRATION_FAILED",
                "The account could not be created.",
                errors);
        }

        try
        {
            await emailVerificationService.SendOtpAsync(
                user.Id,
                cancellationToken);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send registration verification OTP for user {UserId}.",
                user.Id);
        }

        var response = new AuthenticatedUserDto(
            user.Id,
            user.Email!,
            user.UserName!,
            Array.Empty<string>());

        return ApiResponse<AuthenticatedUserDto>.Ok(response);
    }

    public async Task<ApiResponse<LoginResult>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identifier = request.EmailOrUserName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(identifier) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidCredentials();
        }

        var user = await userManager.FindByEmailAsync(identifier)
            ?? await userManager.FindByNameAsync(identifier);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return ApiResponse<LoginResult>.Fail(
                "AUTH_ACCOUNT_LOCKED",
                "The account is temporarily locked. Please try again later.");
        }

        if (signInResult.IsNotAllowed)
        {
            return ApiResponse<LoginResult>.Fail(
                "AUTH_EMAIL_NOT_VERIFIED",
                "Your account is not permitted to sign in. Verify your email if required.");
        }

        if (!signInResult.Succeeded)
        {
            return InvalidCredentials();
        }

        if (!user.EmailConfirmed)
        {
            return ApiResponse<LoginResult>.Fail(
                "AUTH_EMAIL_NOT_VERIFIED",
                "Please verify your email before signing in.");
        }

        var assignedRoles = await userManager.GetRolesAsync(user);

        var platformRoles = assignedRoles
            .Where(role =>
                role == Permissions.PlatformRoles.PlatformAdmin)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.UserName!)
        };

        claims.AddRange(
            platformRoles.Select(
                role => new Claim(ClaimTypes.Role, role)));

        var accessToken = jwtTokenService.CreateAccessToken(
            claims,
            out var accessTokenExpiresAtUtc);

        var refreshTokenPair = jwtTokenService.CreateRefreshToken();

        var now = dateTimeProvider.UtcNow;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenPair.HashedToken,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenPair.ExpiresAtUtc
        };

        dbContext.RefreshTokens.Add(refreshToken);

        user.LastLoginAt = now;

        var memberships = await tenantMembershipService
            .GetMembershipsForUserAsync(user.Id, cancellationToken);

        var membershipDtos = memberships
            .Select(m => new TenantSummaryDto(
                m.TenantId,
                m.TenantName,
                m.TenantSlug,
                m.RoleName))
            .ToArray();

        var activeTenant = membershipDtos.FirstOrDefault();

        IReadOnlyList<string> permissions = Array.Empty<string>();

        if (platformRoles.Contains(
            Permissions.PlatformRoles.PlatformAdmin,
            StringComparer.Ordinal))
        {
            permissions = Permissions.AllPlatformPermissions
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        else if (activeTenant is not null &&
                 Permissions.RolePermissions.TryGetValue(
                     activeTenant.RoleName,
                     out var rolePermissions))
        {
            permissions = rolePermissions
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var userDto = new AuthenticatedUserDto(
            user.Id,
            user.Email!,
            user.UserName!,
            platformRoles);

        var authResponse = new AuthResultDto(
            userDto,
            membershipDtos,
            activeTenant,
            permissions);

        await dbContext.SaveChangesAsync(cancellationToken);

        var loginResult = new LoginResult(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshTokenPair.Token,
            refreshTokenPair.ExpiresAtUtc,
            authResponse);

        return ApiResponse<LoginResult>.Ok(loginResult);
    }

    public async Task<ApiResponse<string>> VerifyOtpAsync(
        VerifyEmailOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp))
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_OTP_REQUEST",
                "Email and verification code are required.");
        }

        var verified = await emailVerificationService.VerifyOtpAsync(
            request.Email.Trim(),
            request.Otp.Trim(),
            cancellationToken);

        if (!verified)
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_OTP",
                "The verification code is invalid or expired.");
        }

        return ApiResponse<string>.Ok(
            "Email verified successfully.");
    }

    public async Task<ApiResponse<string>> ResendOtpAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_EMAIL",
                "A valid email address is required.");
        }

        var normalizedEmail = email.Trim();

        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || user.EmailConfirmed)
        {
            return ApiResponse<string>.Ok(
                "If the account requires verification, a new code will be sent.");
        }

        await emailVerificationService.ResendOtpAsync(
            normalizedEmail,
            cancellationToken);

        return ApiResponse<string>.Ok(
            "If the account requires verification, a new code will be sent.");
    }

    private static ApiResponse<LoginResult> InvalidCredentials() =>
        ApiResponse<LoginResult>.Fail(
            "AUTH_INVALID_CREDENTIALS",
            "Invalid email or password.");
}
