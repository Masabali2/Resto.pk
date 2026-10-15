
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    ICurrentUserService currentUserService,
    IEmailNotificationService emailNotificationService,
    IInAppNotificationService inAppNotificationService,
    ApplicationDbContext dbContext,
    IConfiguration configuration,
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

        // Notification hooks: fire only AFTER the successful login has been
        // fully persisted. Both calls swallow delivery failures internally,
        // so a notification problem can never invalidate authentication or
        // alter the login response contract.
        //  - Email security alert via the durable outbox (Event A).
        //  - In-app security alert for the signed-in user.
        await emailNotificationService.NotifySuccessfulLoginAsync(
            user.Id,
            cancellationToken);

        await inAppNotificationService.NotifySuccessfulLoginAsync(
            user.Id,
            cancellationToken);

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

    public async Task<ApiResponse<LoginResult>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return RefreshRejected();
        }

        var tokenHash = HashRefreshToken(refreshToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await TryRotateAsync(
                tokenHash,
                cancellationToken);

            if (result is not null)
            {
                return result;
            }
        }

        return ApiResponse<LoginResult>.Fail(
            "AUTH_REFRESH_CONFLICT",
            "The session could not be refreshed. Please try again.");
    }

    public async Task<ApiResponse<string>> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiResponse<string>.Ok(
                "Signed out.");
        }

        var tokenHash = HashRefreshToken(refreshToken);

        var revoked = await dbContext.RefreshTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    token => token.RevokedAtUtc,
                    dateTimeProvider.UtcNow),
                cancellationToken);

        if (revoked > 0)
        {
            logger.LogInformation(
                "Revoked refresh token {TokenId} on logout.",
                tokenHash);
        }

        return ApiResponse<string>.Ok("Signed out.");
    }

    private async Task<ApiResponse<LoginResult>?> TryRotateAsync(
        string oldTokenHash,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext
                .Database
                .BeginTransactionAsync(cancellationToken);

            var current = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(
                    token => token.TokenHash == oldTokenHash,
                    cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RefreshRejected();
            }

            if (current.RevokedAtUtc is not null ||
                current.ReplacedByTokenHash is not null)
            {
                await RevokeAllForUserAsync(
                    current.UserId,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}. All sessions revoked.",
                    current.UserId);

                return ApiResponse<LoginResult>.Fail(
                    "AUTH_REFRESH_REUSED",
                    "The refresh token has already been used. All sessions were revoked.");
            }

            if (current.ExpiresAtUtc <= dateTimeProvider.UtcNow)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RefreshRejected();
            }

            var user = await userManager.FindByIdAsync(
                current.UserId.ToString());

            if (user is null ||
                !user.EmailConfirmed ||
                await userManager.IsLockedOutAsync(user))
            {
                await transaction.RollbackAsync(cancellationToken);
                return RefreshRejected();
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

            var newPair = jwtTokenService.CreateRefreshToken();

            var affected = await dbContext.RefreshTokens
                .Where(token =>
                    token.Id == current.Id &&
                    token.RevokedAtUtc == null &&
                    token.ReplacedByTokenHash == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        token => token.RevokedAtUtc,
                        dateTimeProvider.UtcNow)
                    .SetProperty(
                        token => token.ReplacedByTokenHash,
                        newPair.HashedToken),
                    cancellationToken);

            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            dbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newPair.HashedToken,
                CreatedAtUtc = dateTimeProvider.UtcNow,
                ExpiresAtUtc = newPair.ExpiresAtUtc
            });

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

            await transaction.CommitAsync(cancellationToken);

            var loginResult = new LoginResult(
                accessToken,
                accessTokenExpiresAtUtc,
                newPair.Token,
                newPair.ExpiresAtUtc,
                authResponse);

            return ApiResponse<LoginResult>.Ok(loginResult);
        });
    }

    public async Task<ApiResponse<AuthResultDto>> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentUserService.UserId is not { } userId)
        {
            return ApiResponse<AuthResultDto>.Fail(
                "AUTH_UNAUTHORIZED",
                "The request does not carry a valid authenticated identity.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null ||
            !user.EmailConfirmed ||
            await userManager.IsLockedOutAsync(user))
        {
            return ApiResponse<AuthResultDto>.Fail(
                "AUTH_UNAUTHORIZED",
                "The account is not available.");
        }

        var assignedRoles = await userManager.GetRolesAsync(user);

        var platformRoles = assignedRoles
            .Where(role =>
                role == Permissions.PlatformRoles.PlatformAdmin)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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

        return ApiResponse<AuthResultDto>.Ok(authResponse);
    }
    public async Task<ApiResponse<string>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string responseMessage =
            "If an account exists with this email, password reset instructions will be sent.";

        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return ApiResponse<string>.Ok(responseMessage);
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed)
        {
            return ApiResponse<string>.Ok(responseMessage);
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"];

        if (!Uri.TryCreate(
                frontendBaseUrl,
                UriKind.Absolute,
                out var frontendUri) ||
            (frontendUri.Scheme != Uri.UriSchemeHttps &&
             !frontendUri.IsLoopback))
        {
            logger.LogError(
                "Frontend:BaseUrl is missing or invalid for password reset.");

            return ApiResponse<string>.Ok(responseMessage);
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var encodedToken =
            Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
                System.Text.Encoding.UTF8.GetBytes(token));

        var resetUrl =
            $"{frontendUri.ToString().TrimEnd('/')}/reset-password" +
            $"?email={Uri.EscapeDataString(user.Email!)}" +
            $"&token={Uri.EscapeDataString(encodedToken)}";

        await emailNotificationService.NotifyPasswordResetAsync(
            user.Id,
            resetUrl,
            cancellationToken);

        return ApiResponse<string>.Ok(responseMessage);
    }
    public async Task<ApiResponse<string>> ResetPasswordAsync(
     ResetPasswordRequest request,
     CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_PASSWORD_RESET",
                "Email, reset token, and password fields are required.");
        }

        var email = request.Email.Trim();

        if (!string.Equals(
            request.NewPassword,
            request.ConfirmPassword,
            StringComparison.Ordinal))
        {
            return ApiResponse<string>.Fail(
                "AUTH_PASSWORD_MISMATCH",
                "Password and confirmation password do not match.");
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed)
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_PASSWORD_RESET",
                "The reset request is invalid or expired.");
        }

        string decodedToken;

        try
        {
            var tokenBytes =
                Microsoft.AspNetCore.WebUtilities.WebEncoders
                    .Base64UrlDecode(request.Token.Trim());

            decodedToken = System.Text.Encoding.UTF8.GetString(tokenBytes);

            if (string.IsNullOrWhiteSpace(decodedToken))
            {
                return ApiResponse<string>.Fail(
                    "AUTH_INVALID_PASSWORD_RESET",
                    "The reset request is invalid or expired.");
            }
        }
        catch (FormatException)
        {
            return ApiResponse<string>.Fail(
                "AUTH_INVALID_PASSWORD_RESET",
                "The reset request is invalid or expired.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var resetResult = await userManager.ResetPasswordAsync(
            user,
            decodedToken,
            request.NewPassword);

        if (!resetResult.Succeeded)
        {
            var errors = resetResult.Errors
                .Select(error => new ValidationError(
                    "Password",
                    error.Description))
                .ToList();

            return ApiResponse<string>.Fail(
                "AUTH_PASSWORD_RESET_FAILED",
                "The password could not be reset. The token may be invalid or expired, or the password may not meet the requirements.",
                errors);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var revokedAtUtc = dateTimeProvider.UtcNow;

        await dbContext.RefreshTokens
            .Where(token =>
                token.UserId == user.Id &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        token => token.RevokedAtUtc,
                        revokedAtUtc),
                cancellationToken);

        logger.LogInformation(
            "Password reset completed and active refresh tokens revoked for user {UserId}.",
            user.Id);

        return ApiResponse<string>.Ok(
            "Your password has been reset successfully.");
    }
    private async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    token => token.RevokedAtUtc,
                    dateTimeProvider.UtcNow),
                cancellationToken);
    }

    private static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawToken)));

    private static ApiResponse<LoginResult> RefreshRejected() =>
        ApiResponse<LoginResult>.Fail(
            "AUTH_INVALID_REFRESH_TOKEN",
            "The refresh token is invalid, expired, or missing.");

    private static ApiResponse<LoginResult> InvalidCredentials() =>
        ApiResponse<LoginResult>.Fail(
            "AUTH_INVALID_CREDENTIALS",
            "Invalid email or password.");
}
