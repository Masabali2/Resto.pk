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
/// HS256 access-token issuance + validation. Tokens carry identity claims and
/// GLOBAL platform roles only. Tenant id / tenant roles are deliberately NOT
/// embedded: they are re-resolved per request from StaffMemberships, which makes
/// stale-token privilege escalation impossible.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(IEnumerable<Claim> claims, out DateTime expiresAtUtc)
    {
        expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshTokenPair CreateRefreshToken()
    {
        // 64 random bytes => opaque single-use token; only its SHA-256 hash is persisted.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var expires = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);
        return new RefreshTokenPair(raw, hashed, expires);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                RequireExpirationTime = true
            }, out _);
        }
        catch (SecurityTokenException)
        {
            return null; // expired / tampered / wrong issuer — caller returns 401
        }
    }
}
