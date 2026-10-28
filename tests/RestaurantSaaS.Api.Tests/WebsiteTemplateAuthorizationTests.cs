using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Domain.Entities.Identity;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Guards the authorization boundary of the SHARED website template catalogue.
///
/// The catalogue is a platform resource: every tenant uses the same rows, so
/// only a platform administrator may create, read or change them. Tenants may
/// only DISCOVER the active templates through the tenant-facing endpoint.
///
/// The policy tests below evaluate the REAL authorization stack of the API
/// (real policy, real requirement, real handler), so a change that weakens the
/// policy fails here instead of at runtime.
/// </summary>
public sealed class WebsiteTemplateAuthorizationTests
{
    [Fact]
    public async Task PlatformAdminOnly_DeniesAnUnauthenticatedCaller()
    {
        var authorization = BuildAuthorizationService(isPlatformAdmin: false);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await authorization.AuthorizeAsync(
            principal, null, IdentityPolicyNames.PlatformAdminOnly);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task PlatformAdminOnly_DeniesATenantUser()
    {
        var authorization = BuildAuthorizationService(isPlatformAdmin: false);
        var principal = BuildPrincipal(roles: [Permissions.TenantRoles.Owner]);

        var result = await authorization.AuthorizeAsync(
            principal, null, IdentityPolicyNames.PlatformAdminOnly);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task PlatformAdminOnly_DeniesATenantUserWithoutAnyRole()
    {
        var authorization = BuildAuthorizationService(isPlatformAdmin: false);
        var principal = BuildPrincipal(roles: []);

        var result = await authorization.AuthorizeAsync(
            principal, null, IdentityPolicyNames.PlatformAdminOnly);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task PlatformAdminOnly_AllowsAPlatformAdministrator()
    {
        var authorization = BuildAuthorizationService(isPlatformAdmin: true);
        var principal = BuildPrincipal(roles: [Permissions.PlatformRoles.PlatformAdmin]);

        var result = await authorization.AuthorizeAsync(
            principal, null, IdentityPolicyNames.PlatformAdminOnly);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void TemplateManagementController_IsGuardedByThePlatformAdminPolicy()
    {
        var authorize = typeof(WebsiteTemplatesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        authorize.Should().Contain(a => a.Policy == IdentityPolicyNames.PlatformAdminOnly);
    }

    [Fact]
    public void TemplateManagementController_KeepsItsOwnRouteTree()
    {
        var route = typeof(WebsiteTemplatesController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        route.Template.Should().Be("api/v1/website-templates");
    }

    [Fact]
    public void TemplateManagementController_ExposesCreateReadAndUpdate()
    {
        var verbs = typeof(WebsiteTemplatesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => HttpVerbsOf(method))
            .ToList();

        verbs.Should().Contain("GET");
        verbs.Should().Contain("POST");
        verbs.Should().Contain("PUT");
    }

    [Fact]
    public void TenantCatalogueEndpoint_IsNotRestrictedToPlatformAdministrators()
    {
        var action = typeof(WebsitesController)
            .GetMethod(nameof(WebsitesController.ListTemplates))!;

        var authorize = action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        // The tenant catalogue is protected by a TENANT permission, never by
        // the platform-admin policy.
        authorize.Should().NotContain(a => a.Policy == IdentityPolicyNames.PlatformAdminOnly);
        authorize.Should().Contain(a =>
            a.Policy == IdentityPolicyNames.ForPermission(Permissions.WebsiteView));
    }

    [Fact]
    public void TenantController_ExposesTheCatalogueAsReadOnly()
    {
        // A tenant must have no route at all to change the shared catalogue:
        // the only template-related action is the read-only catalogue.
        var templateActions = typeof(WebsitesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.Contains("Template", StringComparison.Ordinal))
            .ToList();

        templateActions.Should().ContainSingle()
            .Which.Should().BeSameAs(typeof(WebsitesController).GetMethod(
                nameof(WebsitesController.ListTemplates)));

        HttpVerbsOf(templateActions.Single()).Should().Equal("GET");
    }

    [Fact]
    public void TenantController_DoesNotExposeTemplateCreateOrUpdateActions()
    {
        var actionNames = typeof(WebsitesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToList();

        actionNames.Should().NotContain(name =>
            name.Contains("Template", StringComparison.Ordinal)
            && name.Contains("Create", StringComparison.Ordinal));
        actionNames.Should().NotContain(name =>
            name.Contains("Template", StringComparison.Ordinal)
            && name.Contains("Update", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> HttpVerbsOf(MethodInfo method) =>
        method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Cast<HttpGetAttribute>()
            .Select(_ => "GET")
            .Concat(method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Cast<HttpPostAttribute>()
                .Select(_ => "POST"))
            .Concat(method.GetCustomAttributes(typeof(HttpPutAttribute), inherit: true).Cast<HttpPutAttribute>()
                .Select(_ => "PUT"))
            .Concat(method.GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true).Cast<HttpDeleteAttribute>()
                .Select(_ => "DELETE"))
            .ToList();

    private static ClaimsPrincipal BuildPrincipal(IReadOnlyCollection<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "tester@resto.pk")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>
    /// Builds the REAL authorization stack of the API (policies, requirements
    /// and handlers) over a single in-memory user, so the policy is evaluated
    /// exactly as it is at runtime.
    /// </summary>
    private static IAuthorizationService BuildAuthorizationService(bool isPlatformAdmin)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(BuildUserManager(isPlatformAdmin));
        services.AddSingleton(Mock.Of<ICurrentUserService>());
        services.AddSingleton(Mock.Of<ITenantContext>());
        services.AddSingleton(Mock.Of<ITenantMembershipService>());

        services.AddTenantAuthorization();

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static UserManager<User> BuildUserManager(bool isPlatformAdmin)    {
        var store = new Mock<IUserStore<User>>();

        var userManager = new Mock<UserManager<User>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<User>>.Instance)
        {
            CallBase = false
        };

        userManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) =>
                new User { Id = Guid.Parse(id), UserName = "tester" });

        userManager
            .Setup(x => x.IsInRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync((User _, string roleName) =>
                isPlatformAdmin && roleName == Permissions.PlatformRoles.PlatformAdmin);

        return userManager.Object;
    }
}
