using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Backend-managed anonymous shopper session service.
///
/// Token design (self-contained, server-verifiable):
///   rawToken = base64url(sessionId) + "." + base64url(expiryUtcTicks) + "." + base64url(hmac)
///   where hmac = HMAC-SHA256(sessionId + "." + expiry, secretKey)
///
/// The raw token is never logged or returned through API responses. Only its
/// SHA-256 hash (64 lowercase hex chars) is exposed, suitable for storage in
/// Cart.SessionId.
///
/// A session is valid independently of whether a cart exists.
/// </summary>
public sealed class ShopperSessionService(
    IOptions<ShopperSessionOptions> options,
    IDateTimeProvider dateTimeProvider) : IShopperSessionService
{
    private const int SessionIdBytes = 32;
    private const int HashLengthHex = 64;

    public (string RawToken, string Hash) CreateSession()
    {
        var sessionIdBytes = RandomNumberGenerator.GetBytes(SessionIdBytes);
        var sessionId = Base64UrlEncode(sessionIdBytes);

        var expiryUtc = dateTimeProvider.UtcNow.AddHours(options.Value.LifetimeHours);
        var expiryTicks = Base64UrlEncode(BitConverter.GetBytes(expiryUtc.Ticks));

        var signingInput = $"{sessionId}.{expiryTicks}";
        var signature = Base64UrlEncode(ComputeHmac(signingInput));

        var rawToken = $"{signingInput}.{signature}";
        var hash = Sha256Hex(rawToken);

        return (rawToken, hash);
    }

    public string? ValidateAndGetHash(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var parts = rawToken.Split('.');
        if (parts.Length != 3)
            return null;

        var sessionId = parts[0];
        var expiryEncoded = parts[1];
        var signature = parts[2];

        // Verify HMAC integrity (constant-time comparison)
        var signingInput = $"{sessionId}.{expiryEncoded}";
        var expectedSignature = Base64UrlEncode(ComputeHmac(signingInput));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return null;
        }

        // Verify expiry
        long expiryTicks;
        try
        {
            var expiryBytes = Base64UrlDecode(expiryEncoded);
            expiryTicks = BitConverter.ToInt64(expiryBytes);
        }
        catch
        {
            return null;
        }

        var expiryUtc = DateTime.FromBinary(expiryTicks);
        if (expiryUtc.Kind != DateTimeKind.Utc)
            expiryUtc = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);
        if (dateTimeProvider.UtcNow >= expiryUtc)
            return null;

        return Sha256Hex(rawToken);
    }

    private byte[] ComputeHmac(string input)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.Secret);
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
    }

    private static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
