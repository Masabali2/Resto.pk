using FluentAssertions;
using RestaurantSaaS.Application.Features.Tenants.DTOs;
using RestaurantSaaS.Application.Features.Tenants.Validators;

namespace RestaurantSaaS.Application.Tests;

public class TenantValidatorTests
{
    private readonly CreateTenantRequestValidator _create = new();
    private readonly UpdateTenantRequestValidator _update = new();
    private readonly UpdateTenantStatusRequestValidator _status = new();

    [Fact]
    public void Create_WhenNameMissing_Fails()
    {
        var result = _create.Validate(new CreateTenantRequest("", "valid-slug"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTenantRequest.Name));
    }

    [Fact]
    public void Create_WhenSlugMissing_Fails()
    {
        var result = _create.Validate(new CreateTenantRequest("Dhaba", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTenantRequest.Slug));
    }

    [Theory]
    [InlineData("-bad-slug")]
    [InlineData("bad-slug-")]
    [InlineData("ab")]
    [InlineData("invalid_slug")]
    public void Create_WhenSlugMalformed_Fails(string slug)
    {
        var result = _create.Validate(new CreateTenantRequest("Dhaba", slug));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTenantRequest.Slug));
    }

    [Fact]
    public void Create_WhenNameTooLong_Fails()
    {
        var result = _create.Validate(new CreateTenantRequest(new string('n', 151), "valid-slug"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = _create.Validate(new CreateTenantRequest("Karachi Dhaba", "karachi-dhaba"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WhenNameEmpty_Fails()
    {
        var result = _update.Validate(new UpdateTenantRequest(""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_WhenValid_Succeeds()
    {
        var result = _update.Validate(new UpdateTenantRequest("New Name"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    public void Status_WhenAllowedValue_Succeeds(string status)
    {
        var result = _status.Validate(new UpdateTenantStatusRequest(status));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("active")]
    [InlineData("Deleted")]
    [InlineData("")]
    public void Status_WhenNotAllowedValue_Fails(string status)
    {
        var result = _status.Validate(new UpdateTenantStatusRequest(status));

        result.IsValid.Should().BeFalse();
    }
}
