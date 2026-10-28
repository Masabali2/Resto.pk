using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Models;
using RestaurantSaaS.Application.Features.Websites.DTOs;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Contract of the ANONYMOUS public template catalogue.
///
/// The endpoint exists so the marketing site can display the active templates
/// before a visitor signs in. These tests pin the properties that make that
/// safe: explicit anonymous access, read-only surface, a public-only DTO, and
/// no change to the protected administrative/tenant endpoints.
/// </summary>
public sealed class PublicWebsiteTemplatesControllerTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();

    [Fact]
    public void Controller_IsExplicitlyAnonymous()
    {
        var allowAnonymous = typeof(PublicWebsiteTemplatesController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);

        allowAnonymous.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_CarriesNoAuthorizePolicy()
    {
        var authorize = typeof(PublicWebsiteTemplatesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        authorize.Should().BeEmpty();
    }

    [Fact]
    public void Controller_UsesThePublicCatalogueRoute()
    {
        var route = typeof(PublicWebsiteTemplatesController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        route.Template.Should().Be("api/v1/public/website-templates");
    }

    [Fact]
    public void Controller_IsStrictlyReadOnly()
    {
        var verbs = typeof(PublicWebsiteTemplatesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(HttpVerbsOf)
            .ToList();

        verbs.Should().Equal("GET");
    }

    [Fact]
    public async Task List_ReturnsActiveTemplatesInTheSuccessEnvelope()
    {
        var expected = new List<PublicWebsiteTemplateDto>
        {
            new(TemplateId, "Spice Garden", "Desi", "spice-garden", "{\"schemaVersion\":1}")
        };

        var templates = new Mock<IWebsiteTemplateService>();
        templates
            .Setup(x => x.ListPublicAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new PublicWebsiteTemplatesController(templates.Object);

        var result = await controller.List(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeOfType<ApiResponse<IReadOnlyList<PublicWebsiteTemplateDto>>>().Subject;

        envelope.Success.Should().BeTrue();
        envelope.Data.Should().BeSameAs(expected);

        templates.Verify(
            x => x.ListPublicAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void PublicDto_ExposesOnlyPublicDisplayFields()
    {
        var properties = typeof(PublicWebsiteTemplateDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        properties.Should().BeEquivalentTo(
            "Id",
            "Name",
            "Description",
            "TemplateKey",
            "DesignConfigurationJson");

        properties.Should().NotContain(
            "PreviewImageFileId",
            "IsActive",
            "CreatedAt");
    }

    /// <summary>
    /// End-to-end check over a real ASP.NET Core pipeline (routing, endpoint
    /// metadata, authorization middleware and System.Text.Json). No database is
    /// involved: the template service is stubbed.
    /// </summary>
    [Fact]
    public async Task Endpoint_ServesAnAnonymousRequest_WithOnlyPublicFields()
    {
        var templates = new Mock<IWebsiteTemplateService>();
        templates
            .Setup(x => x.ListPublicAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PublicWebsiteTemplateDto>
            {
                new(
                    TemplateId,
                    "Spice Garden",
                    "Desi",
                    "spice-garden",
                    "{\"schemaVersion\":1}")
            });

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(PublicWebsiteTemplatesController).Assembly);
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(templates.Object);

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        var client = app.GetTestClient();

        // Deliberately no Authorization header.
        var response = await client.GetAsync("/api/v1/public/website-templates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync();

        payload.Should().Contain("\"success\":true");
        payload.Should().Contain("\"templateKey\":\"spice-garden\"");
        payload.Should().Contain("\"designConfigurationJson\"");
        payload.Should().NotContain("previewImageFileId");
        payload.Should().NotContain("\"isActive\"");
        payload.Should().NotContain("createdAt");
    }

    private static IEnumerable<string> HttpVerbsOf(MethodInfo method)
    {
        if (method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Any())
            yield return "GET";

        if (method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any())
            yield return "POST";

        if (method.GetCustomAttributes(typeof(HttpPutAttribute), inherit: true).Any())
            yield return "PUT";

        if (method.GetCustomAttributes(typeof(HttpPatchAttribute), inherit: true).Any())
            yield return "PATCH";

        if (method.GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true).Any())
            yield return "DELETE";
    }
}
