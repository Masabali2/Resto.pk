using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class ShopperSessionServiceTests
{
    private static ShopperSessionOptions CreateOptions(
        int lifetimeHours = 24,
        string secret = "this-is-a-test-secret-key-that-is-at-least-32-chars-long!")
    {
        return new ShopperSessionOptions
        {
            LifetimeHours = lifetimeHours,
            Secret = secret,
            SameSite = "Lax",
            Secure = true,
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ShopperSessionService CreateService(
        int lifetimeHours = 24,
        string secret = "this-is-a-test-secret-key-that-is-at-least-32-chars-long!",
        DateTime? utcNow = null)
    {
        var options = Options.Create(CreateOptions(lifetimeHours, secret));
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(utcNow ?? DateTime.UtcNow);

        return new ShopperSessionService(options, dateTimeProvider.Object);
    }

    [Fact]
    public void CreateSession_GeneratesThreePartToken()
    {
        var service = CreateService();

        var (rawToken, hash) = service.CreateSession();

        rawToken.Should().NotBeNullOrWhiteSpace();
        var parts = rawToken.Split('.');
        parts.Should().HaveCount(3);
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().HaveLength(64);
        hash.Should().Be(rawToken.ComputeSha256Hex());
    }

    [Fact]
    public void CreateSession_Twice_ProducesDifferentTokens()
    {
        var service = CreateService();

        var (token1, _) = service.CreateSession();
        var (token2, _) = service.CreateSession();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ValidateAndGetHash_WithValidToken_ReturnsHash()
    {
        var service = CreateService();
        var (rawToken, expectedHash) = service.CreateSession();

        var hash = service.ValidateAndGetHash(rawToken);

        hash.Should().Be(expectedHash);
    }

    [Fact]
    public void ValidateAndGetHash_WithNull_ReturnsNull()
    {
        var service = CreateService();

        var hash = service.ValidateAndGetHash(null);

        hash.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WithEmpty_ReturnsNull()
    {
        var service = CreateService();

        var hash = service.ValidateAndGetHash("");

        hash.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WithMalformedToken_ReturnsNull()
    {
        var service = CreateService();

        service.ValidateAndGetHash("not-a-valid-token").Should().BeNull();
        service.ValidateAndGetHash("only.two.parts").Should().BeNull();
        service.ValidateAndGetHash("a.b.c.d").Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WithTamperedSignature_ReturnsNull()
    {
        var service = CreateService();
        var (rawToken, _) = service.CreateSession();

        var parts = rawToken.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.tampered-signature";

        service.ValidateAndGetHash(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WithExpiredToken_ReturnsNull()
    {
        var now = DateTime.UtcNow;
        var service = CreateService(lifetimeHours: 1, utcNow: now);

        var (rawToken, _) = service.CreateSession();

        // Advance time past expiry
        var future = now.AddHours(2);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(future);
        var options = Options.Create(CreateOptions(1, "this-is-a-test-secret-key-that-is-at-least-32-chars-long!"));
        var expiredService = new ShopperSessionService(options, dateTimeProvider.Object);

        expiredService.ValidateAndGetHash(rawToken).Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WithDifferentSecret_ReturnsNull()
    {
        var serviceA = CreateService(secret: "secret-one-this-is-at-least-32-chars-long!!!");
        var serviceB = CreateService(secret: "secret-two-this-is-at-least-32-chars-long!!!");

        var (rawToken, _) = serviceA.CreateSession();

        serviceB.ValidateAndGetHash(rawToken).Should().BeNull();
    }

    [Fact]
    public void CreateSession_HashIsSha256OfRawToken()
    {
        var service = CreateService();
        var (rawToken, hash) = service.CreateSession();

        rawToken.ComputeSha256Hex().Should().Be(hash);
    }
}

internal static class Sha256HexExtensions
{
    public static string ComputeSha256Hex(this string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
