
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Issues and validates JWT access tokens and generates opaque refresh tokens.
/// Access tokens contain identity claims and global platform roles only.
/// Tenant memberships and tenant permissions are resolved separately.
/// </summary>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(
        IEnumerable<Claim> claims,
        out DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var now = dateTimeProvider.UtcNow;

        expiresAtUtc = now.AddMinutes(
            _options.AccessTokenExpirationMinutes);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.Secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshTokenPair CreateRefreshToken()
    {
        // Generate an opaque, cryptographically random token.
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Base64UrlEncoder.Encode(randomBytes);

        // Persist only this hash, never the raw token.
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var expiresAtUtc = dateTimeProvider.UtcNow.AddDays(
            _options.RefreshTokenExpirationDays);

        return new RefreshTokenPair(
            rawToken,
            tokenHash,
            expiresAtUtc);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.Secret));

        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,

                    ValidateAudience = true,
                    ValidAudience = _options.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,

                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,

                    ClockSkew = TimeSpan.FromSeconds(30)
                },
                out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // Malformed token input.
            return null;
        }
    }
}