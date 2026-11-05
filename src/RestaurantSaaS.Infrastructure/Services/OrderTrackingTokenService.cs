using System.Security.Cryptography;
using System.Text;
using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Services;

public sealed class OrderTrackingTokenService : IOrderTrackingTokenService
{
    public (string RawToken, string Hash) Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var hash = Hash(rawToken);
        return (rawToken, hash);
    }

    public string Hash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ArgumentException("Tracking token is required.", nameof(rawToken));

        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
