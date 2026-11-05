using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Infrastructure.Services;

namespace RestaurantSaaS.Infrastructure.Tests;

public class ShopperSessionServiceTests
{
    private static ShopperSessionOptions CreateOptions(int lifetimeHours = 24, string? secret = null)
    {
        return new ShopperSessionOptions
        {
            CookieName = "resto_shopper_session",
            LifetimeHours = lifetimeHours,
            Secret = secret ?? "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SameSite = "Lax",
            Secure = true,
            AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https://localhost:4200" }
        };
    }

    private static ShopperSessionService CreateService(int lifetimeHours = 24, string? secret = null)
    {
        var options = Options.Create(CreateOptions(lifetimeHours, secret));
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
        return new ShopperSessionService(options, dateTimeProvider.Object);
    }

    [Fact]
    public void CreateSession_ReturnsNonEmptyRawToken()
    {
        var service = CreateService();
        var (rawToken, hash) = service.CreateSession();
        rawToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateSession_Returns64CharHexHash()
    {
        var service = CreateService();
        var (_, hash) = service.CreateSession();
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void CreateSession_RawTokenIsNotReturnedAsHash()
    {
        var service = CreateService();
        var (rawToken, hash) = service.CreateSession();
        rawToken.Should().NotBe(hash);
    }

    [Fact]
    public void ValidateAndGetHash_ValidToken_ReturnsHash()
    {
        var service = CreateService();
        var (rawToken, hash) = service.CreateSession();
        var result = service.ValidateAndGetHash(rawToken);
        result.Should().Be(hash);
    }

    [Fact]
    public void ValidateAndGetHash_NullToken_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ValidateAndGetHash(null);
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_EmptyToken_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ValidateAndGetHash("");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_WhitespaceToken_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ValidateAndGetHash("   ");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_MalformedToken_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ValidateAndGetHash("not-a-valid-token");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_TamperedToken_ReturnsNull()
    {
        var service = CreateService();
        var (rawToken, _) = service.CreateSession();
        var tampered = rawToken + "extra";
        var result = service.ValidateAndGetHash(tampered);
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAndGetHash_ExpiredToken_ReturnsNull()
    {
        var options = Options.Create(CreateOptions(lifetimeHours: 24));
        var expiredTime = DateTime.UtcNow.AddHours(-1);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.UtcNow).Returns(expiredTime);
        var service = new ShopperSessionService(options, dateTimeProvider.Object);

        var (rawToken, _) = service.CreateSession();

        // Now advance time past expiry
        dateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow.AddHours(25));
        var result = service.ValidateAndGetHash(rawToken);
        result.Should().BeNull();
    }

    [Fact]
    public void CreateSession_Twice_ProducesDifferentTokens()
    {
        var service = CreateService();
        var (rawToken1, _) = service.CreateSession();
        var (rawToken2, _) = service.CreateSession();
        rawToken1.Should().NotBe(rawToken2);
    }

    [Fact]
    public void CreateSession_TokenContainsSessionIdAndExpiry()
    {
        var service = CreateService();
        var (rawToken, _) = service.CreateSession();
        var parts = rawToken.Split('.');
        parts.Should().HaveCount(3);
        parts[0].Should().NotBeNullOrWhiteSpace();
        parts[1].Should().NotBeNullOrWhiteSpace();
        parts[2].Should().NotBeNullOrWhiteSpace();
    }
}
