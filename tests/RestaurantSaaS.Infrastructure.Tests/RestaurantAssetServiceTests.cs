using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RestaurantSaaS.Application.Common.Exceptions;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Restaurants.DTOs;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class RestaurantAssetServiceTests : IDisposable
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private readonly ApplicationDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<IFileStorageService> _storage;
    private readonly StubMembershipService _memberships;
    private readonly TenantAccessGuard _accessGuard;

    private readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public RestaurantAssetServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"resto-assets-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId);
        _db = new ApplicationDbContext(options, _tenantContext, new DateTimeProvider());

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUser.Setup(x => x.UserId).Returns(_userId);

        _memberships = new StubMembershipService();
        _storage = new Mock<IFileStorageService>();
        _storage.Setup(x => x.SaveAsync(It.IsAny<Guid?>(), It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? t, Stream s, string fn, string ct, CancellationToken _) =>
                $"tenant/{t:N}/{Guid.NewGuid():N}{Path.GetExtension(fn)}");
        _storage.Setup(x => x.GetPresignedUrl(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns((string key, TimeSpan _) => $"/api/v1/files/{key}?exp=3600");
        _storage.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _accessGuard = new TenantAccessGuard(_db, _tenantContext, _currentUser.Object, _memberships);
    }

    public void Dispose() => _db.Dispose();

    private RestaurantAssetService Service() =>
        new(_db, _tenantContext, _currentUser.Object, _memberships, _storage.Object, _accessGuard);

    private IFormFile PngFile(byte[]? bytes = null, string name = "photo.png", string contentType = "image/png")
    {
        bytes ??= Convert.FromBase64String(TinyPngBase64);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", name);
        file.Headers = new HeaderDictionary { ["Content-Type"] = contentType };
        return file;
    }

    private async Task AddActiveSubscriptionAsync()
    {
        var sub = Subscription.Start(_tenantId, Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), null);
        sub.GetType().GetProperty("Id")!.SetValue(sub, Guid.NewGuid());
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();
    }

    private async Task AddOwnerMembershipAsync()
    {
        var tenant = Tenant.Create("Tenant", "tenant");
        tenant.ChangeStatus(TenantStatus.Active);
        tenant.GetType().GetProperty("Id")!.SetValue(tenant, _tenantId);
        _db.Tenants.Add(tenant);

        var membership = StaffMembership.Create(_tenantId, _userId, Guid.NewGuid());
        membership.GetType().GetProperty("Id")!.SetValue(membership, Guid.NewGuid());
        _db.StaffMemberships.Add(membership);
        _memberships.Memberships.Add(new TenantMembershipInfo(
            membership.Id, _tenantId, "Tenant", "tenant", Guid.NewGuid(), "Owner",
            (int)MembershipStatus.Active));
        _memberships.AddMembership(membership.Id, _userId);
        await _db.SaveChangesAsync();
    }

    private async Task<Guid> AddRestaurantAsync(Guid restaurantId)
    {
        var address = Domain.ValueObjects.Address.Create("1 Main St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(_tenantId, "R1", address);
        restaurant.GetType().GetProperty("Id")!.SetValue(restaurant, restaurantId);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        _db.Restaurants.Add(restaurant);
        await _db.SaveChangesAsync();
        return restaurantId;
    }

    private async Task<FileMetadata> AddFileAsync(Guid fileId, string fileName, long size)
    {
        var file = FileMetadata.Create(
            _tenantId, fileName, $"tenant/x/{fileId:N}.png", "image/png", size, "local", 100, 100);
        file.GetType().GetProperty("Id")!.SetValue(file, fileId);
        _db.Files.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    private async Task<Guid> AddAssetAsync(Guid assetId, Guid restaurantId, Guid fileId, AssetType type,
        bool isActive = true)
    {
        var asset = RestaurantAsset.Create(restaurantId, fileId, type);
        asset.GetType().GetProperty("Id")!.SetValue(asset, assetId);
        if (!isActive)
            asset.SetActive(false);
        _db.RestaurantAssets.Add(asset);
        await _db.SaveChangesAsync();
        return assetId;
    }

    [Fact]
    public async Task Upload_DarkLogo_SucceedsAndPersistsAssetType()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var result = await Service().UploadAsync(
            restaurantId, "darklogo", PngFile().OpenReadStream(), "logo.png", "image/png");

        result.Should().NotBeNull();
        result.AssetType.Should().Be("DarkLogo");
        result.FileName.Should().Be("logo.png");
        result.ContentType.Should().Be("image/png");
        result.Id.Should().NotBe(Guid.Empty);

        var stored = await _db.RestaurantAssets
            .Include(a => a.File)
            .SingleAsync(a => a.Id == result.Id);
        stored.AssetType.Should().Be(AssetType.DarkLogo);
        stored.IsActive.Should().BeTrue();
        stored.FileId.Should().Be(result.FileId);
    }

    [Fact]
    public async Task Upload_OGBanner_SucceedsAndPersistsAssetType()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var result = await Service().UploadAsync(
            restaurantId, "ogbanner", PngFile().OpenReadStream(), "og.png", "image/png");

        result.AssetType.Should().Be("OGBanner");
        var stored = await _db.RestaurantAssets.SingleAsync(a => a.Id == result.Id);
        stored.AssetType.Should().Be(AssetType.OGBanner);
    }

    [Fact]
    public async Task Upload_HeroBanner_SucceedsAndPersistsAssetType()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var result = await Service().UploadAsync(
            restaurantId, "herobanner", PngFile().OpenReadStream(), "hero.png", "image/png");

        result.AssetType.Should().Be("HeroBanner");
        var stored = await _db.RestaurantAssets.SingleAsync(a => a.Id == result.Id);
        stored.AssetType.Should().Be(AssetType.HeroBanner);
    }

    [Fact]
    public async Task Upload_InvalidAssetType_Throws()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var act = () => Service().UploadAsync(
            restaurantId, "menuitemimage", PngFile().OpenReadStream(), "x.png", "image/png");

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("ASSET_TYPE_INVALID");
    }

    [Fact]
    public async Task Upload_Unauthenticated_Throws()
    {
        _currentUser.Setup(x => x.IsAuthenticated).Returns(false);
        _currentUser.Setup(x => x.UserId).Returns((Guid?)null);
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var act = () => Service().UploadAsync(
            restaurantId, "logo", PngFile().OpenReadStream(), "x.png", "image/png");

        await Assert.ThrowsAsync<NotAuthenticatedException>(act);
    }

    [Fact]
    public async Task Upload_ReplacesPreviousActiveAssetOfSameType()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var oldFile = await AddFileAsync(Guid.NewGuid(), "old.png", 100);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, oldFile.Id, AssetType.Logo, isActive: true);

        var result = await Service().UploadAsync(
            restaurantId, "logo", PngFile().OpenReadStream(), "new.png", "image/png");

        var assets = await _db.RestaurantAssets.Where(a => a.RestaurantId == restaurantId).ToListAsync();
        assets.Should().HaveCount(2);
        assets.Where(a => a.IsActive).Should().HaveCount(1);
        assets.Single(a => a.IsActive).FileId.Should().Be(result.FileId);
        assets.Single(a => !a.IsActive).FileId.Should().Be(oldFile.Id);
    }

    [Fact]
    public async Task List_ReturnsRestaurantAssetsWithMetadata()
    {
        await AddOwnerMembershipAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var file = await AddFileAsync(Guid.NewGuid(), "logo.png", 1234);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, file.Id, AssetType.Logo);

        var list = await Service().ListAsync(restaurantId);

        list.Should().HaveCount(1);
        var dto = list[0];
        dto.Id.Should().NotBe(Guid.Empty);
        dto.AssetType.Should().Be("Logo");
        dto.FileId.Should().Be(file.Id);
        dto.FileName.Should().Be("logo.png");
        dto.ContentType.Should().Be("image/png");
        dto.FileSize.Should().Be(1234);
        dto.Url.Should().NotBeNullOrWhiteSpace();
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task List_ExcludesInactiveAssets()
    {
        await AddOwnerMembershipAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var file = await AddFileAsync(Guid.NewGuid(), "logo.png", 100);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, file.Id, AssetType.Logo, isActive: false);

        var list = await Service().ListAsync(restaurantId);
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnreferencedAsset_ReturnsTrueAndSoftDeletesFile()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var file = await AddFileAsync(Guid.NewGuid(), "logo.png", 100);
        var assetId = await AddAssetAsync(Guid.NewGuid(), restaurantId, file.Id, AssetType.Logo);

        var deleted = await Service().DeleteAsync(restaurantId, assetId);

        deleted.Should().BeTrue();
        var stored = await _db.Files.FindAsync(file.Id);
        stored!.IsDeleted.Should().BeTrue();
        var asset = await _db.RestaurantAssets.FindAsync(assetId);
        asset!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ReferencedAsset_ReturnsFalseAndKeepsEverything()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var file = await AddFileAsync(Guid.NewGuid(), "logo.png", 100);
        var assetId = await AddAssetAsync(Guid.NewGuid(), restaurantId, file.Id, AssetType.Logo);

        var websiteId = Guid.NewGuid();
        var website = Website.Create(_tenantId, restaurantId, Guid.NewGuid(), "Site", "site");
        website.GetType().GetProperty("Id")!.SetValue(website, websiteId);
        var configJson = "{\"schemaVersion\":1,\"settings\":{\"assets\":{\"Logo\":\"" + assetId + "\"}}}";
        var config = WebsiteConfiguration.CreateDraft(websiteId, configJson, 1);
        config.GetType().GetProperty("Id")!.SetValue(config, Guid.NewGuid());
        _db.Websites.Add(website);
        _db.WebsiteConfigurations.Add(config);
        await _db.SaveChangesAsync();

        var deleted = await Service().DeleteAsync(restaurantId, assetId);

        deleted.Should().BeFalse();
        var asset = await _db.RestaurantAssets.FindAsync(assetId);
        asset!.IsActive.Should().BeTrue();
        var stored = await _db.Files.FindAsync(file.Id);
        stored!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NotFound_Throws()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var act = () => Service().DeleteAsync(restaurantId, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetStorageUsage_ReturnsDistinctUsedBytesAndNullQuota()
    {
        await AddOwnerMembershipAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var fileA = await AddFileAsync(Guid.NewGuid(), "a.png", 1000);
        var fileB = await AddFileAsync(Guid.NewGuid(), "b.png", 2000);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, fileA.Id, AssetType.Logo);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, fileB.Id, AssetType.Favicon);
        // A second asset pointing to the same file must not double-count.
        await AddAssetAsync(Guid.NewGuid(), restaurantId, fileA.Id, AssetType.DarkLogo);
        // Deleted file must not count.
        var deletedFile = await AddFileAsync(Guid.NewGuid(), "deleted.png", 9999);
        deletedFile.MarkDeleted(DateTime.UtcNow);
        await AddAssetAsync(Guid.NewGuid(), restaurantId, deletedFile.Id, AssetType.HeroBanner);

        var usage = await Service().GetStorageUsageAsync(restaurantId);

        usage.UsedBytes.Should().Be(3000); // fileA + fileB, fileA counted once
        usage.QuotaBytes.Should().BeNull();
    }

    [Fact]
    public async Task GetStorageUsage_CrossTenantFiles_NotCounted()
    {
        await AddOwnerMembershipAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());
        var tenantFile = await AddFileAsync(Guid.NewGuid(), "tenant.png", 5000);
        // file belongs to the resolved tenant but is not linked to this restaurant
        await AddAssetAsync(Guid.NewGuid(), restaurantId, tenantFile.Id, AssetType.Logo);

        var usage = await Service().GetStorageUsageAsync(restaurantId);

        usage.UsedBytes.Should().Be(5000);
        usage.QuotaBytes.Should().BeNull();
    }

    [Fact]
    public async Task Delete_CrossTenantAsset_ReturnsNotFound()
    {
        await AddOwnerMembershipAsync();
        await AddActiveSubscriptionAsync();
        var restaurantId = await AddRestaurantAsync(Guid.NewGuid());

        var otherTenantFile = await AddFileAsync(Guid.NewGuid(), "other.png", 100);
        // asset exists but belongs to a different restaurant; ownership check hides it
        var act = () => Service().DeleteAsync(restaurantId, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(act);
    }
}
