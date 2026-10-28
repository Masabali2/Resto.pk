using FluentAssertions;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Tests;

/// <summary>
/// Domain rules of the website template design configuration.
///
/// The domain owns the INVARIANTS of the stored document (present, bounded,
/// well-formed JSON). The deeper shape rules are owned by the Application
/// layer schema validator, so they are covered by
/// <c>WebsiteTemplateDesignSchemaTests</c> in the Application test project.
/// </summary>
public sealed class WebsiteTemplateEntityTests
{
    private const string ValidConfiguration = """
    {"schemaVersion":1,"design":{"layout":{"header":"centered"}},"sections":[{"type":"hero","enabled":true,"order":1}]}
    """;

    private const string OtherConfiguration = """
    {"schemaVersion":1,"design":{"layout":{"header":"left"}},"sections":[{"type":"hero","enabled":true,"order":1}]}
    """;

    [Fact]
    public void Create_NormalizesTheTemplateKey()
    {
        var template = WebsiteTemplate.Create("Classic", "  CLASSIC  ");

        template.TemplateKey.Should().Be("classic");
        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_LeavesTheDesignConfigurationUnset()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        // A template that was never given a design configuration is still a
        // valid entity: the column is nullable and the client falls back to a
        // safe "no preview yet" state.
        template.DesignConfigurationJson.Should().BeNull();
    }

    [Fact]
    public void SetDesignConfiguration_WithValidConfiguration_StoresTheDocument()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        template.SetDesignConfiguration(ValidConfiguration);

        template.DesignConfigurationJson.Should().Be(ValidConfiguration);
        template.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetDesignConfiguration_WithNull_Throws()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        var act = () => template.SetDesignConfiguration(null!);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_REQUIRED");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void SetDesignConfiguration_WithEmptyConfiguration_Throws(string configuration)
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        var act = () => template.SetDesignConfiguration(configuration);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_REQUIRED");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"schemaVersion\":")]
    [InlineData("not json at all")]
    public void SetDesignConfiguration_WithMalformedJson_Throws(string configuration)
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        var act = () => template.SetDesignConfiguration(configuration);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_INVALID");
    }

    [Fact]
    public void SetDesignConfiguration_WithOversizedConfiguration_Throws()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");
        var oversized = new string('a', WebsiteTemplate.MaxDesignConfigurationLength + 1);

        var act = () => template.SetDesignConfiguration(oversized);

        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "TEMPLATE_DESIGN_CONFIGURATION_TOO_LARGE");
    }

    [Fact]
    public void SetDesignConfiguration_WithConfigurationAtTheLimit_IsAccepted()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");
        var atLimit = "{\"pad\":\"" + new string('a', WebsiteTemplate.MaxDesignConfigurationLength - 10) + "\"}";

        atLimit.Length.Should().Be(WebsiteTemplate.MaxDesignConfigurationLength);

        template.SetDesignConfiguration(atLimit);

        template.DesignConfigurationJson.Should().Be(atLimit);
    }

    [Fact]
    public void SetDesignConfiguration_WithTheSameConfiguration_DoesNotTouchTheTimestamp()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");
        template.SetDesignConfiguration(ValidConfiguration);

        var updatedAt = template.UpdatedAt;
        updatedAt.Should().NotBeNull();

        // The clock has to move on, otherwise "not updated" and "updated to
        // the same value" could not be told apart.
        WaitUntilClockPasses(updatedAt!.Value);

        template.SetDesignConfiguration(ValidConfiguration);

        template.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void SetDesignConfiguration_WithADifferentConfiguration_UpdatesTheTimestamp()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");
        template.SetDesignConfiguration(ValidConfiguration);

        var updatedAt = template.UpdatedAt;

        WaitUntilClockPasses(updatedAt!.Value);

        template.SetDesignConfiguration(OtherConfiguration);

        template.UpdatedAt.Should().BeAfter(updatedAt.Value);
        template.DesignConfigurationJson.Should().Be(OtherConfiguration);
    }

    [Fact]
    public void SetDesignConfiguration_AcceptsAnyWellFormedJson_ShapeIsCheckedByTheApplicationLayer()
    {
        var template = WebsiteTemplate.Create("Classic", "classic");

        // The domain only guarantees "well-formed JSON"; the allowlist schema
        // lives in the Application layer and is enforced on the request.
        template.SetDesignConfiguration("[]");

        template.DesignConfigurationJson.Should().Be("[]");
    }

    private static void WaitUntilClockPasses(DateTime moment)
    {
        while (DateTime.UtcNow <= moment)
        {
            Thread.Sleep(5);
        }
    }
}
