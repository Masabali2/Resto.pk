using FluentAssertions;
using RestaurantSaaS.Application.Features.Websites.DTOs;
using RestaurantSaaS.Application.Features.Websites.Validators;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Application.Tests;

/// <summary>
/// Request validation of the website template catalogue.
///
/// The design configuration is validated by the allowlist schema validator of
/// the Application layer, so the rules below only have to decide WHETHER a
/// configuration has to be supplied — never the shape of the document itself.
/// </summary>
public sealed class WebsiteTemplateRequestValidatorTests
{
    private const string ValidConfiguration = """
    {"schemaVersion":1,"category":"Fine dining","tags":["premium"],"featured":true,"displayOrder":2,
    "design":{"palette":{"background":"#F7F2EA","surface":"#FFFFFF","text":"#1C1B1A","mutedText":"#6B6560",
    "primary":"#2E2A27","accent":"#B0672A","border":"#E3D9CB"},
    "typography":{"headingFont":"serif-editorial","bodyFont":"sans-modern"},
    "layout":{"header":"centered","hero":"cinematic","menu":"editorial-list","gallery":"strip",
    "footer":"simple","buttons":"outline","corners":"sharp"},
    "sections":[{"type":"hero","enabled":true,"order":1}]},
    "previewContent":{"eyebrow":"An evening","heroTitle":"Ember, Oak & Patience",
    "heroDescription":"Seasonal plates over live fire.","primaryActionLabel":"Reserve a Table",
    "sampleDishes":[{"name":"Dry-Aged Ribeye","price":"PKR 5,400"}]}}
    """;

    private readonly CreateWebsiteTemplateRequestValidator _create = new();
    private readonly UpdateWebsiteTemplateRequestValidator _update = new();

    // ---------------------------------------------------------------- create

    [Fact]
    public void Create_WithValidConfiguration_Succeeds()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Spice Garden", "spice-garden", "Desi", null, ValidConfiguration));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutConfiguration_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Spice Garden", "spice-garden", "Desi", null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyConfiguration_Fails(string configuration)
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Spice Garden", "spice-garden", "Desi", null, configuration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Create_WithMalformedJson_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Broken", "broken", null, null, "{ not json"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Create_WithOversizedConfiguration_Fails()
    {
        var oversized = new string('a', WebsiteTemplate.MaxDesignConfigurationLength + 1);

        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Too big", "too-big", null, null, oversized));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Create_WithUnknownProperty_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Unknown", "unknown", null, null, """{"schemaVersion":1,"evil":true}"""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Create_WithUnsupportedLayoutVariant_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Bad layout", "bad-layout", null, null,
            """{"schemaVersion":1,"design":{"layout":{"header":"diagonal"}}}"""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Create_WithoutName_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "", "spice-garden", "Desi", null, ValidConfiguration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateWebsiteTemplateRequest.Name));
    }

    [Fact]
    public void Create_WithMalformedTemplateKey_Fails()
    {
        var result = _create.Validate(new CreateWebsiteTemplateRequest(
            "Spice Garden", "invalid key!", "Desi", null, ValidConfiguration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateWebsiteTemplateRequest.TemplateKey));
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public void Update_WithoutConfiguration_Succeeds()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            "Renamed", null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithOnlyActiveState_Succeeds()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, false));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithValidConfiguration_Succeeds()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null, ValidConfiguration));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithoutAnyField_Fails()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one field"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithEmptyConfiguration_Fails(string configuration)
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null, configuration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Update_WithMalformedJson_Fails()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null, "{ not json"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Update_WithOversizedConfiguration_Fails()
    {
        var oversized = new string('a', WebsiteTemplate.MaxDesignConfigurationLength + 1);

        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null, oversized));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateWebsiteTemplateRequest.DesignConfigurationJson));
    }

    [Fact]
    public void Update_WithInvalidShape_Fails()
    {
        var result = _update.Validate(new UpdateWebsiteTemplateRequest(
            null, null, null, """{"schemaVersion":1,"design":{"typography":{"headingFont":"comic"}}}"""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateWebsiteTemplateRequest.DesignConfigurationJson));
    }
}
