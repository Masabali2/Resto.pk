using System.Security.Claims;

namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Abstraction so Application/test code never depends on JWT plumbing.
/// Implemented in Infrastructure (JwtTokenService).
/// SECURITY: tokens carry identity + GLOBAL platform roles only. Tenant and
/// tenant-role are NEVER baked into the token; they are re-validated from
/// StaffMemberships on every request.
/// </summary>
public interface IJwtTokenService
{
    string CreateAccessToken(IEnumerable<Claim> claims, out DateTime expiresAtUtc);

    /// <summary>Opaque refresh token (random, hashed at rest, single-use rotation).</summary>
    RefreshTokenPair CreateRefreshToken();

    ClaimsPrincipal? ValidateAccessToken(string token);
}

public sealed record RefreshTokenPair(string Token, string HashedToken, DateTime ExpiresAtUtc);
