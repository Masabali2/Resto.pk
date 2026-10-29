using FluentAssertions;
using RestaurantSaaS.Application.Common;
using Xunit;

namespace RestaurantSaaS.Application.Tests;

public sealed class WebsiteConfigurationSchemaFontsTests
{
    [Fact]
    public void FontFamilies_ContainsSupportedBrandingFonts()
    {
        WebsiteConfigSchema.FontFamilies.Should().Contain("plus-jakarta-sans");
        WebsiteConfigSchema.FontFamilies.Should().Contain("outfit");
        WebsiteConfigSchema.FontFamilies.Should().Contain("inter");
        WebsiteConfigSchema.FontFamilies.Should().Contain("playfair-display");
    }

    [Theory]
    [InlineData("plus-jakarta-sans")]
    [InlineData("outfit")]
    [InlineData("inter")]
    [InlineData("playfair-display")]
    public void Validate_AcceptsAllowedFont(string font)
    {
        var json = "{\"schemaVersion\":1,\"theme\":{\"colors\":{\"primary\":\"#F97316\"},\"font\":\"" + font + "\"}}";
        var error = WebsiteConfigSchema.Validate(json);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsDisallowedFont()
    {
        var json = "{\"schemaVersion\":1,\"theme\":{\"font\":\"comic-sans\"}}";
        var error = WebsiteConfigSchema.Validate(json);

        error.Should().NotBeNull();
        error.Should().Contain("theme.font");
    }
}
