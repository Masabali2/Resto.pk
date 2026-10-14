
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class EmailVerificationService(
    UserManager<User> userManager,
    ApplicationDbContext dbContext,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailVerificationOptions> verificationOptions,
    ILogger<EmailVerificationService> logger)
    : IEmailVerificationService
{
    public async Task SendOtpAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new InvalidOperationException(
                "The user could not be found.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException(
                "The user does not have an email address.");
        }

        var now = dateTimeProvider.UtcNow;

        var recentOtps = await dbContext.EmailVerificationOtps
            .Where(x =>
                x.UserId == user.Id &&
                x.CreatedAtUtc > now.AddHours(-1))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (recentOtps.Count > 0)
        {
            var nextAllowedAt = recentOtps[0].CreatedAtUtc.AddSeconds(
                verificationOptions.Value.ResendCooldownSeconds);

            if (now < nextAllowedAt)
            {
                var retryAfterSeconds = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (nextAllowedAt - now).TotalSeconds));

                throw new OtpRateLimitException(
                    "Please wait before requesting another verification code.",
                    retryAfterSeconds);
            }
        }

        if (recentOtps.Count >=
            verificationOptions.Value.MaxSendsPerHour)
        {
            var nextAllowedAt = recentOtps
                .OrderBy(x => x.CreatedAtUtc)
                .First()
                .CreatedAtUtc.AddHours(1);

            var retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(
                    (nextAllowedAt - now).TotalSeconds));

            throw new OtpRateLimitException(
                "You have requested too many verification codes. Please try again later.",
                retryAfterSeconds);
        }

        await dbContext.EmailVerificationOtps
            .Where(x =>
                x.UserId == user.Id &&
                x.UsedAtUtc == null &&
                x.VerifiedAtUtc == null &&
                x.InvalidatedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.InvalidatedAtUtc,
                    now),
                cancellationToken);

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6");

        var otpRecord = new EmailVerificationOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpHash = ComputeOtpHash(otp),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(
                verificationOptions.Value.ExpirationMinutes),
            FailedAttempts = 0
        };

        dbContext.EmailVerificationOtps.Add(otpRecord);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(
                ex,
                "Unable to persist verification OTP for user {UserId}.",
                user.Id);

            throw;
        }

        var safeName = HtmlEncoder.Default.Encode(
            user.FullName ?? string.Empty);

        var safeOtp = HtmlEncoder.Default.Encode(otp);

        var htmlBody = $"""
            <html>
            <body>
                <h2>Verify your Resto.pk email</h2>
                <p>Hello {safeName},</p>
                <p>Your email verification code is:</p>
                <h1>{safeOtp}</h1>
                <p>This code expires in {verificationOptions.Value.ExpirationMinutes} minutes.</p>
                <p>If you did not create this account, you can ignore this email.</p>
            </body>
            </html>
            """;

        try
        {
            await emailService.SendAsync(
                user.Email,
                "Your Resto.pk verification code",
                htmlBody,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unable to send verification OTP to user {UserId}.",
                user.Id);

            throw;
        }
    }

    public async Task<bool> VerifyOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(otp) ||
            otp.Length != 6 ||
            !otp.All(char.IsAsciiDigit))
        {
            return false;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        var now = dateTimeProvider.UtcNow;

        var otpRecord = await dbContext.EmailVerificationOtps
            .Where(x =>
                x.UserId == user.Id &&
                x.UsedAtUtc == null &&
                x.VerifiedAtUtc == null &&
                x.InvalidatedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpRecord is null)
        {
            return false;
        }

        if (otpRecord.ExpiresAtUtc <= now ||
            otpRecord.FailedAttempts >=
            verificationOptions.Value.MaxAttempts)
        {
            otpRecord.InvalidatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var submittedHash = Convert.FromHexString(
            ComputeOtpHash(otp));

        var storedHash = Convert.FromHexString(
            otpRecord.OtpHash);

        if (!CryptographicOperations.FixedTimeEquals(
                submittedHash,
                storedHash))
        {
            otpRecord.FailedAttempts++;
            otpRecord.LastAttemptAtUtc = now;

            if (otpRecord.FailedAttempts >=
                verificationOptions.Value.MaxAttempts)
            {
                otpRecord.InvalidatedAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            var claimed = await dbContext.EmailVerificationOtps
                .Where(x =>
                    x.Id == otpRecord.Id &&
                    x.UsedAtUtc == null &&
                    x.VerifiedAtUtc == null &&
                    x.InvalidatedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.VerifiedAtUtc, now)
                    .SetProperty(x => x.UsedAtUtc, now)
                    .SetProperty(x => x.LastAttemptAtUtc, now),
                    cancellationToken);

            if (claimed == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            user.EmailConfirmed = true;

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            await dbContext.EmailVerificationOtps
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Id != otpRecord.Id &&
                    x.UsedAtUtc == null &&
                    x.VerifiedAtUtc == null &&
                    x.InvalidatedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.InvalidatedAtUtc, now),
                    cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return user.EmailConfirmed;
    }

    public async Task ResendOtpAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await SendOtpAsync(user.Id, cancellationToken);
    }

    private string ComputeOtpHash(string otp)
    {
        var key = Encoding.UTF8.GetBytes(
            verificationOptions.Value.HmacKey);

        var data = Encoding.UTF8.GetBytes(otp);

        var hash = HMACSHA256.HashData(key, data);

        return Convert.ToHexString(hash);
    }
}
