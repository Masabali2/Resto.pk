using FluentAssertions;
using RestaurantSaaS.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace RestaurantSaaS.Infrastructure.Tests;

public class OrderTrackingTokenServiceTests
{
    private readonly OrderTrackingTokenService _service = new();

    [Fact]
    public void Generate_ReturnsNonEmptyBase64UrlToken()
    {
        var (rawToken, hash) = _service.Generate();
        rawToken.Should().NotBeNullOrWhiteSpace();
        rawToken.Should().NotContain("+");
        rawToken.Should().NotContain("/");
        rawToken.Should().NotContain("=");
    }

    [Fact]
    public void Generate_TokenRepresents32Bytes()
    {
        var (rawToken, hash) = _service.Generate();
        var padded = rawToken.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
        var bytes = Convert.FromBase64String(padded);
        bytes.Length.Should().Be(32);
    }

    [Fact]
    public void Generate_HashEqualsHashOfRawToken()
    {
        var (rawToken, hash) = _service.Generate();
        var computed = _service.Hash(rawToken);
        computed.Should().Be(hash);
    }

    [Fact]
    public void Hash_Produces64CharLowercaseHexSha256()
    {
        var hash = _service.Hash("test-token");
        hash.Length.Should().Be(64);
        Regex.IsMatch(hash, "^[0-9a-f]{64}$").Should().BeTrue();
    }

    [Fact]
    public void Generate_CalledSeparately_ProducesDifferentTokensAndHashes()
    {
        var (raw1, hash1) = _service.Generate();
        var (raw2, hash2) = _service.Generate();
        raw1.Should().NotBe(raw2);
        hash1.Should().NotBe(hash2);
    }
}
