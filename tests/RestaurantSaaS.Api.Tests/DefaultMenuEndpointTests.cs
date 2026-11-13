using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Menus.DTOs;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Authorization;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Guards the default-menu read contract over a real ASP.NET Core pipeline.
///
/// The endpoint must stay on its canonical route and keep the tenant
/// <c>Menu.View</c> permission policy. Tenant authorization must use the
/// membership of the SELECTED tenant (the X-Tenant-Id candidate), never the
/// first membership on the user. These tests exercise routing, authentication,
/// tenant resolution, the real authorization handler and exception mapping.
/// </summary>
public sealed class DefaultMenuEndpointTests
{
    private const string TestScheme = "Test";
    private const string TestUserHeader = "X-Test-User";
    private const string TenantHeader = AuthenticatedTenantResolver.TenantHeaderName;

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid TenantC = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    private static readonly Guid OwnerUser = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ManagerUser = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid NoMenuUser = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static readonly DefaultMenuDetailsDto DefaultMenu = new(
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
        "Demo Menu",
        "Platform default catalogue",
        MenuStatus.Published.ToString(),
        1,
        1,
        Array.Empty<DefaultMenuCategoryDto>());

    private static readonly MenuDto PublishedMenu = new(
        Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
        TenantA,
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "Published Menu",
        null,
        true,
        MenuStatus.Published.ToString(),
        DateTime.UtcNow,
        null,
        null);

    private static readonly MenuDto UnpublishedMenu = new(
        Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
        TenantA,
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "Published Menu",
        null,
        true,
        MenuStatus.Draft.ToString(),
        DateTime.UtcNow,
        null,
        null);

    [Fact]
    public void DefaultMenuAction_HasTheCanonicalRouteAndTenantPermissionPolicy()
    {
        var controllerRoute = typeof(MenusController)
            .GetCustomAttribute<RouteAttribute>(inherit: true);

        controllerRoute.Should().NotBeNull();
        controllerRoute!.Template.Should().Be("api/v1");

        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.GetDefault),
            BindingFlags.Public | BindingFlags.Instance);

        action.Should().NotBeNull();

        var httpGet = action!
            .GetCustomAttributes<HttpGetAttribute>(inherit: true)
            .Single();

        httpGet.Template.Should().Be("menus/default");

        var authorize = action
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        authorize.Policy.Should().Be(
            IdentityPolicyNames.ForPermission(Permissions.MenuView));

        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void DefaultMenuAction_IsReadOnly()
    {
        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.GetDefault),
            BindingFlags.Public | BindingFlags.Instance)!;

        action.GetCustomAttributes<HttpGetAttribute>(inherit: true)
            .Should()
            .ContainSingle();

        action.GetCustomAttributes<HttpPostAttribute>(inherit: true)
            .Should()
            .BeEmpty();
        action.GetCustomAttributes<HttpPutAttribute>(inherit: true)
            .Should()
            .BeEmpty();
        action.GetCustomAttributes<HttpPatchAttribute>(inherit: true)
            .Should()
            .BeEmpty();
        action.GetCustomAttributes<HttpDeleteAttribute>(inherit: true)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void PublishAction_HasTheCanonicalRouteAndMenuUpdatePermissionPolicy()
    {
        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.Publish),
            BindingFlags.Public | BindingFlags.Instance);

        action.Should().NotBeNull();

        var httpPatch = action!
            .GetCustomAttributes<HttpPatchAttribute>(inherit: true)
            .Single();

        httpPatch.Template.Should().Be("menus/{menuId:guid}/publish");

        var authorize = action
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        authorize.Policy.Should().Be(
            IdentityPolicyNames.ForPermission(Permissions.MenuUpdate));

        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void PublishAction_IsPatchOnly()
    {
        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.Publish),
            BindingFlags.Public | BindingFlags.Instance)!;

        action.GetCustomAttributes<HttpPatchAttribute>(inherit: true)
            .Should()
            .ContainSingle();
        action.GetCustomAttributes<HttpGetAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpPostAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpPutAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpDeleteAttribute>(inherit: true).Should().BeEmpty();
    }

    [Fact]
    public void UnpublishAction_HasTheCanonicalRouteAndMenuUpdatePermissionPolicy()
    {
        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.Unpublish),
            BindingFlags.Public | BindingFlags.Instance);

        action.Should().NotBeNull();

        var httpPatch = action!
            .GetCustomAttributes<HttpPatchAttribute>(inherit: true)
            .Single();

        httpPatch.Template.Should().Be("menus/{menuId:guid}/unpublish");

        var authorize = action
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        authorize.Policy.Should().Be(
            IdentityPolicyNames.ForPermission(Permissions.MenuUpdate));

        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void UnpublishAction_IsPatchOnly()
    {
        var action = typeof(MenusController).GetMethod(
            nameof(MenusController.Unpublish),
            BindingFlags.Public | BindingFlags.Instance)!;

        action.GetCustomAttributes<HttpPatchAttribute>(inherit: true)
            .Should()
            .ContainSingle();
        action.GetCustomAttributes<HttpGetAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpPostAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpPutAttribute>(inherit: true).Should().BeEmpty();
        action.GetCustomAttributes<HttpDeleteAttribute>(inherit: true).Should().BeEmpty();
    }

    [Fact]
    public async Task Endpoint_Returns401_WhenUnauthenticated()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.Client.GetAsync("/api/v1/menus/default");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Returns200_ForAnOwnerInTheSelectedTenant()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.GetDefaultAsync(OwnerUser, TenantA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("\"success\":true");
        payload.Should().Contain("\"name\":\"Demo Menu\"");
        harness.Recorder.Calls.Should().Be(1);
        harness.Recorder.SelectedTenant.Should().Be(TenantA);
    }

    [Fact]
    public async Task Endpoint_Returns200_ForAManagerInTheSelectedTenant()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.GetDefaultAsync(ManagerUser, TenantA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        harness.Recorder.SelectedTenant.Should().Be(TenantA);
    }

    [Fact]
    public async Task Endpoint_Returns403_WhenTheSelectedTenantRoleLacksMenuView()
    {
        await using var harness = await Harness.CreateAsync();

        // The owner is a member of Tenant B, but their role there does not
        // grant Menu.View. The selected tenant must drive authorization.
        var response = await harness.GetDefaultAsync(OwnerUser, TenantB);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Returns403_ForAUserWithoutMenuView()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.GetDefaultAsync(NoMenuUser, TenantA);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Returns403_WhenTheHeaderSelectsANonMemberTenant()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.GetDefaultAsync(OwnerUser, TenantC);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Returns404_WhenTheDefaultMenuSourceIsMissing()
    {
        await using var harness = await Harness.CreateAsync(sourceMissing: true);

        var response = await harness.GetDefaultAsync(OwnerUser, TenantA);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("\"errorCode\":\"NOT_FOUND\"");
    }

    [Fact]
    public async Task Endpoint_DoesNotAcceptWriteVerbs()
    {
        await using var harness = await Harness.CreateAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/menus/default");
        request.Headers.Add(TestUserHeader, OwnerUser.ToString());
        request.Headers.Add(TenantHeader, TenantA.ToString());

        var response = await harness.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Publish_Returns200_ForAnOwner()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.PublishAsync(OwnerUser, TenantA, PublishedMenu.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("\"success\":true");
        payload.Should().Contain("\"status\":\"Published\"");
        harness.Recorder.Calls.Should().Be(1);
        harness.Recorder.SelectedTenant.Should().Be(TenantA);
    }

    [Fact]
    public async Task Endpoint_Unpublish_Returns200_ForAnOwner()
    {
        await using var harness = await Harness.CreateAsync();

        var response = await harness.UnpublishAsync(OwnerUser, TenantA, UnpublishedMenu.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("\"success\":true");
        payload.Should().Contain("\"status\":\"Draft\"");
        harness.Recorder.Calls.Should().Be(1);
        harness.Recorder.SelectedTenant.Should().Be(TenantA);
    }

    [Fact]
    public async Task Endpoint_Publish_Returns401_WhenUnauthenticated()
    {
        await using var harness = await Harness.CreateAsync();

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/menus/{PublishedMenu.Id}/publish");
        var response = await harness.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        harness.Recorder.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Endpoint_Publish_Returns403_WhenTheSelectedTenantRoleLacksMenuUpdate()
    {
        await using var harness = await Harness.CreateAsync();

        // Owner has a restricted role (no MenuUpdate) in Tenant B.
        var response = await harness.PublishAsync(OwnerUser, TenantB, PublishedMenu.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        harness.Recorder.Calls.Should().Be(0);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private Harness(WebApplication app, HttpClient client, MenuCallRecorder recorder)
        {
            _app = app;
            Client = client;
            Recorder = recorder;
        }

        public HttpClient Client { get; }

        public MenuCallRecorder Recorder { get; }

        public static async Task<Harness> CreateAsync(bool sourceMissing = false)
        {
            var recorder = new MenuCallRecorder();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<TenantContext>();
            builder.Services.AddScoped<ITenantContext>(
                services => services.GetRequiredService<TenantContext>());
            builder.Services.AddScoped<ITenantContextSetter>(
                services => services.GetRequiredService<TenantContext>());

            builder.Services.AddSingleton<ITenantMembershipService>(
                new FakeTenantMembershipService());
            builder.Services.AddScoped<ITenantResolver, AuthenticatedTenantResolver>();

            builder.Services.AddSingleton(recorder);
            builder.Services.AddScoped<IMenuService>(services =>
            {
                var tenantContext = services.GetRequiredService<ITenantContext>();
                var menus = new Mock<IMenuService>();

                if (sourceMissing)
                {
                    menus
                        .Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new NotFoundException("Default menu was not found."));
                }
                else
                {
                    menus
                        .Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() =>
                        {
                            recorder.Calls++;
                            recorder.SelectedTenant = tenantContext.TenantId;
                            return DefaultMenu;
                        });
                }

                menus
                    .Setup(x => x.PublishAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        recorder.Calls++;
                        recorder.SelectedTenant = tenantContext.TenantId;
                        return PublishedMenu;
                    });

                menus
                    .Setup(x => x.UnpublishAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        recorder.Calls++;
                        recorder.SelectedTenant = tenantContext.TenantId;
                        return UnpublishedMenu;
                    });

                return menus.Object;
            });

            builder.Services.AddSingleton(Mock.Of<IMenuCategoryService>());
            builder.Services.AddSingleton(Mock.Of<IMenuItemService>());
            builder.Services.AddSingleton(BuildUserManager());
            builder.Services.AddTenantAuthorization();

            builder.Services
                .AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestScheme,
                    _ => { });

            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(MenusController).Assembly);

            var app = builder.Build();

            app.UseMiddleware<HandleExceptionMiddleware>();
            app.UseAuthentication();
            app.UseMiddleware<TenantResolutionMiddleware>();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            return new Harness(app, app.GetTestClient(), recorder);
        }

        public Task<HttpResponseMessage> GetDefaultAsync(Guid userId, Guid tenantId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/v1/menus/default");

            request.Headers.Add(TestUserHeader, userId.ToString());
            request.Headers.Add(TenantHeader, tenantId.ToString());

            return Client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PublishAsync(Guid userId, Guid tenantId, Guid menuId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/v1/menus/{menuId}/publish");

            request.Headers.Add(TestUserHeader, userId.ToString());
            request.Headers.Add(TenantHeader, tenantId.ToString());

            return Client.SendAsync(request);
        }

        public Task<HttpResponseMessage> UnpublishAsync(Guid userId, Guid tenantId, Guid menuId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/v1/menus/{menuId}/unpublish");

            request.Headers.Add(TestUserHeader, userId.ToString());
            request.Headers.Add(TenantHeader, tenantId.ToString());

            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }

        private static UserManager<User> BuildUserManager()
        {
            var store = new Mock<IUserStore<User>>();

            return new Mock<UserManager<User>>(
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
            }.Object;
        }
    }

    private sealed class MenuCallRecorder
    {
        public int Calls { get; set; }

        public Guid? SelectedTenant { get; set; }
    }

    private sealed class FakeTenantMembershipService : ITenantMembershipService
    {
        // The owner has a permissive role in Tenant A and a restricted role in
        // Tenant B, proving authorization follows the SELECTED workspace.
        private static readonly IReadOnlyDictionary<(Guid UserId, Guid TenantId), string> Roles =
            new Dictionary<(Guid UserId, Guid TenantId), string>
            {
                [(OwnerUser, TenantA)] = Permissions.TenantRoles.Owner,
                [(OwnerUser, TenantB)] = "RestrictedRole",
                [(ManagerUser, TenantA)] = Permissions.TenantRoles.Manager,
                [(NoMenuUser, TenantA)] = "RestrictedRole"
            };

        public Task<TenantMembershipInfo?> GetMembershipAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            if (!Roles.TryGetValue((userId, tenantId), out var roleName))
            {
                return Task.FromResult<TenantMembershipInfo?>(null);
            }

            return Task.FromResult<TenantMembershipInfo?>(
                new TenantMembershipInfo(
                    Guid.NewGuid(),
                    tenantId,
                    $"Tenant {tenantId:N}",
                    $"tenant-{tenantId:N}",
                    Guid.NewGuid(),
                    roleName,
                    (int)MembershipStatus.Active));
        }

        public Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var memberships = Roles
                .Where(pair => pair.Key.UserId == userId)
                .Select(pair => new TenantMembershipInfo(
                    Guid.NewGuid(),
                    pair.Key.TenantId,
                    $"Tenant {pair.Key.TenantId:N}",
                    $"tenant-{pair.Key.TenantId:N}",
                    Guid.NewGuid(),
                    pair.Value,
                    (int)MembershipStatus.Active))
                .ToList();

            return Task.FromResult<IReadOnlyList<TenantMembershipInfo>>(memberships);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestUserHeader, out var userId) ||
                !Guid.TryParse(userId.ToString(), out var parsedUserId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, parsedUserId.ToString()),
                new Claim(ClaimTypes.Name, "default-menu-test-user")
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, TestScheme));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, TestScheme)));
        }
    }
}
