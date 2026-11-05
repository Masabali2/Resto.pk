using FluentAssertions;
using RestaurantSaaS.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace RestaurantSaaS.Infrastructure.Tests;

public class OrderNumberGeneratorTests
{
    private readonly OrderNumberGenerator _generator = new();

    [Fact]
    public void Generate_HasExpectedFormat()
    {
        var number = _generator.Generate();
        number.Should().MatchRegex(@"^ORD-\d{8}-[0-9A-F]{16}$");
    }

    [Fact]
    public void Generate_DoesNotExceed50Characters()
    {
        var number = _generator.Generate();
        number.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void Generate_RepeatedCalls_ProduceDifferentIdentifiers()
    {
        var numbers = Enumerable.Range(0, 10).Select(_ => _generator.Generate()).ToList();
        numbers.Distinct().Count().Should().BeGreaterThan(1);
    }
}
