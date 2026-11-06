using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Api.Controllers;
using RestaurantSaaS.Api.Extensions;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Guards <see cref="AssetsController.GetFile" /> — the endpoint that serves
/// uploaded menu item images.
///
/// The defect: the previous implementation authorized file delivery against the
/// AMBIENT tenant resolved from the <c>X-Tenant-Id</c> header. Native image
/// requests cannot send that header, so authorized users (especially users with
/// multiple active tenant memberships) received 401/403 instead of the image.
///
/// These tests exercise the real pipeline (authentication, tenant middleware,
/// real authorization attributes, exception mapping) and assert that delivery is
/// bound to the FILE'S owning tenant and the caller's ACTIVE membership in it.
/// </summary>
public sealed class AssetsControllerGetFileTests
{
    private const string TestScheme = "Test";
    private const string TestUserHeader = "X-Test-User";
    private const string TenantHeader = AuthenticatedTenantResolver.TenantHeaderName;

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    // OwnerUser is an ACTIVE member of BOTH tenants (multi-tenant user).
    private static readonly Guid OwnerUser = Guid.Parse("11111111-1111-4111-8111-111111111111");
    // OtherUser is an ACTIVE member of TenantB ONLY.
    private static readonly Guid OtherUser = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static readonly byte[] ImageBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    [Fact]
    public async Task GetFile_Returns200_WithImageBytes_ForAuthorizedFileOwner()
    {
        await using var harness = await Harness.CreateAsync();
        var fileId = await harness.AddFileAsync(TenantA, "photo.jpg", "image/jpeg", ImageBytes);

        var response = await harness.GetFileAsync(OwnerUser, fileId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/jpeg");
        // The response must be usable directly by an <img> element, not
        // presented as a file download.
        response.Content.Headers.ContentDisposition?.DispositionType.Should().Be("inline");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(ImageBytes);
    }

    [Fact]
    public async Task GetFile_Returns200_ForLegacyStorageKeyUrl()
    {
        await using var harness = await Harness.CreateAsync();
        const string storageKey = "tenant/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaa/legacy.png";
        var fileId = await harness.AddFileAsync(
            TenantA, "legacy.png", "image/png", ImageBytes, storageKey: storageKey);

        var response = await harness.GetFileAsync(OwnerUser, storageKey);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(ImageBytes);
        // The lookup must resolve by immutable id when given one, but also keep
        // serving legacy storage-key URLs.
        var byId = await harness.GetFileAsync(OwnerUser, fileId.ToString());
        byId.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFile_Returns200_ForUrlEncodedStorageKeyUrl()
    {
        await using var harness = await Harness.CreateAsync();
        const string storageKey = "tenant/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaa/encoded.png";
        await harness.AddFileAsync(
            TenantA, "encoded.png", "image/png", ImageBytes, storageKey: storageKey);

        // Upload responses and presigned URLs escape the storage key, so the
        // endpoint must resolve the decoded key as well.
        var response = await harness.GetFileAsync(
            OwnerUser, Uri.EscapeDataString(storageKey));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(ImageBytes);
    }

    [Fact]
    public async Task GetFile_Returns404_WhenPhysicalFileIsMissing()
    {
        await using var harness = await Harness.CreateAsync();
        const string storageKey = "tenant/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaa/missing.png";
        var fileId = await harness.AddFileAsync(
            TenantA, "missing.png", "image/png", ImageBytes, storageKey: storageKey);

        harness.DeletePhysicalFile(storageKey);

        var response = await harness.GetFileAsync(OwnerUser, fileId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFile_Returns403_WhenUserIsNotMemberOfFileTenant()
    {
        await using var harness = await Harness.CreateAsync();
        var fileId = await harness.AddFileAsync(TenantA, "photo.jpg", "image/jpeg", ImageBytes);

        // OtherUser is a member of TenantB only, never TenantA.
        var response = await harness.GetFileAsync(OtherUser, fileId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFile_Returns404_WhenFileIsMissing()
    {
        await using var harness = await Harness.CreateAsync();
        var response = await harness.GetFileAsync(OwnerUser, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFile_Returns404_WhenFileIsDeleted()
    {
        await using var harness = await Harness.CreateAsync();
        var fileId = await harness.AddFileAsync(TenantA, "photo.jpg", "image/jpeg", ImageBytes, deleted: true);

        var response = await harness.GetFileAsync(OwnerUser, fileId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFile_Returns401_WhenUnauthenticated()
    {
        await using var harness = await Harness.CreateAsync();
        var fileId = await harness.AddFileAsync(TenantA, "photo.jpg", "image/jpeg", ImageBytes);

        var response = await harness.GetFileAsync(null, fileId.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFile_Returns200_ForMultiTenantUserWithoutTenantHeader()
    {
        await using var harness = await Harness.CreateAsync();
        var fileId = await harness.AddFileAsync(TenantA, "photo.jpg", "image/jpeg", ImageBytes);

        // OwnerUser belongs to both tenants; no X-Tenant-Id header is sent
        // (as native image requests cannot send custom headers). Authorization
        // must still succeed because it is bound to the file's owning tenant,
        // not to the ambient header context.
        var response = await harness.GetFileAsync(OwnerUser, fileId.ToString(), addTenantHeader: false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(ImageBytes);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ApplicationDbContext _db;
        private readonly string _tempRoot;

        public HttpClient Client { get; }

        private Harness(WebApplication app, ApplicationDbContext db, string tempRoot)
        {
            _app = app;
            _db = db;
            _tempRoot = tempRoot;
            Client = app.GetTestClient();
        }

        public static async Task<Harness> CreateAsync()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"resto-files-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var memberships = new FakeTenantMembershipService();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:LocalRoot"] = tempRoot
                })
                .Build();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddSingleton<IConfiguration>(config);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<TenantContext>();
            builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
            builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();

            // AddDbContext builds options per scope, so the database name and
            // root must be created once; otherwise each scope (the seed
            // context and the request context) gets its own empty database.
            var databaseName = $"GetFileTest-{Guid.NewGuid():N}";
            var inMemoryRoot = new InMemoryDatabaseRoot();
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName, inMemoryRoot));

            builder.Services.AddSingleton<IAssetUploadService>(Mock.Of<IAssetUploadService>());
            builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(config));
            builder.Services.AddSingleton<ITenantMembershipService>(memberships);
            builder.Services.AddSingleton<ITenantResolver, AuthenticatedTenantResolver>();
            builder.Services.AddSingleton<ITenantResolver, PublicTenantResolver>();

            // GetFile uses plain [Authorize] (authentication only), no tenant
            // policy, so the tenant-policy handlers (which need UserManager)
            // are not required here.
            builder.Services.AddAuthorization();

            builder.Services
                .AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });

            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(AssetsController).Assembly);

            var app = builder.Build();

            app.UseMiddleware<HandleExceptionMiddleware>();
            app.UseAuthentication();
            app.UseMiddleware<TenantResolutionMiddleware>();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            return new Harness(app, app.Services.GetRequiredService<ApplicationDbContext>(), tempRoot);
        }

        public async Task<Guid> AddFileAsync(
            Guid tenantId,
            string fileName,
            string contentType,
            byte[] bytes,
            string? storageKey = null,
            bool deleted = false)
        {
            var key = storageKey ?? $"tenant/{tenantId:N}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
            var fullPath = Path.Combine(_tempRoot, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, bytes);

            var metadata = FileMetadata.Create(tenantId, fileName, key, contentType, bytes.Length, "local");
            if (deleted)
                metadata.MarkDeleted(DateTime.UtcNow);

            _db.Files.Add(metadata);
            await _db.SaveChangesAsync();

            return metadata.Id;
        }

        public void DeletePhysicalFile(string storageKey)
        {
            var fullPath = Path.Combine(
                _tempRoot,
                storageKey.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        public Task<HttpResponseMessage> GetFileAsync(
            Guid? userId,
            string pathSegment,
            bool addTenantHeader = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{pathSegment}");
            if (userId is { } id)
                request.Headers.Add(TestUserHeader, id.ToString());
            if (addTenantHeader)
                request.Headers.Add(TenantHeader, TenantA.ToString());
            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            try
            {
                Directory.Delete(_tempRoot, true);
            }
            catch
            {
                // Best-effort cleanup of the temporary storage root.
            }
            await _app.DisposeAsync();
        }
    }

    private sealed class FakeTenantMembershipService : ITenantMembershipService
    {
        public Task<TenantMembershipInfo?> GetMembershipAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var active =
                (userId == OwnerUser && (tenantId == TenantA || tenantId == TenantB)) ||
                (userId == OtherUser && tenantId == TenantB);

            if (!active)
                return Task.FromResult<TenantMembershipInfo?>(null);

            return Task.FromResult<TenantMembershipInfo?>(new TenantMembershipInfo(
                Guid.NewGuid(),
                tenantId,
                $"Tenant {tenantId:N}",
                $"tenant-{tenantId:N}",
                Guid.NewGuid(),
                Permissions.TenantRoles.Owner,
                (int)MembershipStatus.Active));
        }

        public Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var list = new List<TenantMembershipInfo>();
            var add = (Guid t) => list.Add(new TenantMembershipInfo(
                Guid.NewGuid(), t, $"Tenant {t:N}", $"tenant-{t:N}", Guid.NewGuid(),
                Permissions.TenantRoles.Owner, (int)MembershipStatus.Active));

            if (userId == OwnerUser)
            {
                add(TenantA);
                add(TenantB);
            }
            else if (userId == OtherUser)
            {
                add(TenantB);
            }

            return Task.FromResult<IReadOnlyList<TenantMembershipInfo>>(list);
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
                new Claim(ClaimTypes.Name, "get-file-test-user")
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestScheme));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, TestScheme)));
        }
    }
}
