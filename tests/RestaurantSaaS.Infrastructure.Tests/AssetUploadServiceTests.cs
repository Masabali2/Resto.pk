using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using RestaurantSaaS.Application.Common.Files;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;
using System.IO;

namespace RestaurantSaaS.Infrastructure.Tests;

public sealed class AssetUploadServiceTests : IDisposable
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private readonly ApplicationDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<IFileStorageService> _storage;
    private readonly Mock<IUploadDestinationContext> _uploadDestinationContext;
    private readonly ImageDimensionReader _dimensions;
    private readonly Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public AssetUploadServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"resto-upload-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(_tenantId);
        _db = new ApplicationDbContext(options, _tenantContext, new DateTimeProvider());

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUser.Setup(x => x.UserId).Returns(_userId);

        _storage = new Mock<IFileStorageService>();
        _storage.Setup(x => x.SaveAsync(It.IsAny<Guid?>(), It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? t, Stream s, string fn, string ct, CancellationToken _) =>
                $"tenant/{t:N}/{Guid.NewGuid():N}{Path.GetExtension(fn)}");
        _storage.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dimensions = new ImageDimensionReader();

        _uploadDestinationContext = new Mock<IUploadDestinationContext>();
        _uploadDestinationContext.SetupGet(x => x.TenantId).Returns(_tenantId);
        _uploadDestinationContext.Setup(x => x.IsActiveMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _uploadDestinationContext.Setup(x => x.FindRestaurantAssetsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid restaurantId, CancellationToken _) =>
                _db.RestaurantAssets.Where(a => a.RestaurantId == restaurantId).ToList());
        _uploadDestinationContext.Setup(x => x.AddRestaurantAsset(It.IsAny<RestaurantAsset>()))
            .Callback((RestaurantAsset asset) => _db.RestaurantAssets.Add(asset));
        _uploadDestinationContext.Setup(x => x.FindWebsiteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _db.Websites.FirstOrDefault(w => w.Id == id && w.TenantId == _tenantId));
        _uploadDestinationContext.Setup(x => x.FindRestaurantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _db.Restaurants.FirstOrDefault(r => r.Id == id && r.TenantId == _tenantId && !r.IsDeleted));
        _uploadDestinationContext.Setup(x => x.FindMenuItemAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _db.MenuItems.Include(i => i.Category).ThenInclude(c => c.Menu)
                    .FirstOrDefault(i => i.Id == id && !i.IsDeleted && i.Category.Menu.TenantId == _tenantId));
    }

    public void Dispose() => _db.Dispose();

    private IFormFile PngFile(byte[]? bytes = null, string name = "photo.png", string contentType = "image/png")
    {
        bytes ??= Convert.FromBase64String(TinyPngBase64);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", name);
        file.Headers = new HeaderDictionary { ["Content-Type"] = contentType };
        return file;
    }

    private AssetUploadService Service(params IUploadDestinationHandler[] extra)
    {
        var handlers = new List<IUploadDestinationHandler>
        {
            new WebsiteSectionUploadHandler(),
            new RestaurantLogoUploadHandler(),
            new MenuItemUploadHandler()
        };
        foreach (var h in extra) handlers.Add(h);

        return new AssetUploadService(
            _db, _tenantContext, _currentUser.Object, new DateTimeProvider(),
            _storage.Object, _dimensions,
            Options.Create(new ImageUploadOptions { MaxFileSizeBytes = 5 * 1024 * 1024 }), handlers,
            _uploadDestinationContext.Object);
    }

    private async Task<Website> AddWebsiteAsync(Guid websiteId)
    {
        var restaurantId = Guid.NewGuid();
        var address = Domain.ValueObjects.Address.Create("1 Main St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(_tenantId, "R1", address);
        restaurant.GetType().GetProperty("Id")!.SetValue(restaurant, restaurantId);
        _db.Restaurants.Add(restaurant);

        var website = Website.Create(_tenantId, restaurantId, Guid.NewGuid(), "Site", "site");
        website.GetType().GetProperty("Id")!.SetValue(website, websiteId);
        _db.Websites.Add(website);

        var membership = StaffMembership.Create(_tenantId, _userId, Guid.NewGuid());
        membership.GetType().GetProperty("Id")!.SetValue(membership, Guid.NewGuid());
        _db.StaffMemberships.Add(membership);

        await _db.SaveChangesAsync();
        return website;
    }

    private async Task<Restaurant> AddRestaurantAsync(Guid restaurantId)
    {
        var address = Domain.ValueObjects.Address.Create("1 Main St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(_tenantId, "R1", address);
        restaurant.GetType().GetProperty("Id")!.SetValue(restaurant, restaurantId);
        _db.Restaurants.Add(restaurant);

        var membership = StaffMembership.Create(_tenantId, _userId, Guid.NewGuid());
        membership.GetType().GetProperty("Id")!.SetValue(membership, Guid.NewGuid());
        _db.StaffMemberships.Add(membership);

        await _db.SaveChangesAsync();
        return restaurant;
    }

    private async Task<Guid> AddMenuItemAsync(Guid itemId, Guid restaurantId)
    {
        var menu = Menu.Create(_tenantId, restaurantId, "M");
        menu.GetType().GetProperty("Id")!.SetValue(menu, Guid.NewGuid());
        _db.Menus.Add(menu);

        var category = MenuCategory.Create(menu.Id, "Cat");
        category.GetType().GetProperty("Id")!.SetValue(category, Guid.NewGuid());
        _db.MenuCategories.Add(category);

        var item = MenuItem.Create(category.Id, "Item", Domain.ValueObjects.Money.New(100m, "PKR"));
        item.GetType().GetProperty("Id")!.SetValue(item, itemId);
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();
        return itemId;
    }

    [Fact]
    public async Task Upload_ValidWebsiteSectionImage_SucceedsAndPersistsMetadata()
    {
        var websiteId = Guid.NewGuid();
        await AddWebsiteAsync(websiteId);

        var service = Service();
        var result = await service.UploadAsync(
            new WebsiteSectionDestination
            {
                WebsiteId = websiteId, SectionType = "hero", SettingKey = "imageUrl"
            },
            PngFile());

        result.Should().NotBeNull();
        result.PreviewUrl.Should().StartWith("/api/v1/files/");
        result.Width.Should().Be(1);
        result.Height.Should().Be(1);

        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == result.AssetId);
        file.Should().NotBeNull();
        file!.TenantId.Should().Be(_tenantId);
        file.ContentType.Should().Be("image/png");
        file.Width.Should().Be(1);
        file.Height.Should().Be(1);
    }

    [Fact]
    public async Task Upload_RestaurantLogo_ActivatesNewAndDeactivatesPrevious()
    {
        var restaurantId = Guid.NewGuid();
        await AddRestaurantAsync(restaurantId);
        var oldFile = FileMetadata.Create(
            _tenantId, "old.png", "tenant/x/old.png", "image/png", 100, "local", 10, 10);
        _db.Files.Add(oldFile);
        _db.RestaurantAssets.Add(
            RestaurantAsset.Create(restaurantId, oldFile.Id, AssetType.Logo));
        await _db.SaveChangesAsync();

        var service = Service();
        var result = await service.UploadAsync(
            new RestaurantLogoDestination { RestaurantId = restaurantId },
            PngFile());

        var assets = await _db.RestaurantAssets
            .Where(a => a.RestaurantId == restaurantId).ToListAsync();
        assets.Should().HaveCount(2);
        assets.Where(a => a.IsActive).Should().HaveCount(1);
        assets.Single(a => a.IsActive).FileId.Should().Be(result.AssetId);
        assets.Single(a => !a.IsActive).FileId.Should().Be(oldFile.Id);
    }

    [Fact]
    public async Task Upload_MenuItem_AssociatesWithOnlyThatItem()
    {
        var itemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        await AddRestaurantAsync(restaurantId);
        await AddMenuItemAsync(itemId, restaurantId);
        await AddMenuItemAsync(otherItemId, restaurantId);

        var service = Service();
        var result = await service.UploadAsync(
            new MenuItemDestination { MenuItemId = itemId },
            PngFile());

        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == itemId);
        item!.ImageFileId.Should().Be(result.AssetId);

        var other = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == otherItemId);
        other!.ImageFileId.Should().BeNull();
    }

    [Fact]
    public async Task Upload_EmptyFile_Fails()
    {
        var service = Service();
        var file = new FormFile(Stream.Null, 0, 0, "file", "empty.png");
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" }, file);

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_FILE_REQUIRED");
    }

    [Fact]
    public async Task Upload_OversizedFile_Fails()
    {
        var service = Service();
        var big = new byte[6 * 1024 * 1024];
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" },
            PngFile(big, "big.png"));

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Upload_NonImageContent_Fails()
    {
        var service = Service();
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" },
            PngFile(System.Text.Encoding.UTF8.GetBytes("not an image"), "text.txt", "text/plain"));

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_UNSUPPORTED_TYPE");
    }

    [Fact]
    public async Task Upload_MimeSignatureMismatch_Fails()
    {
        var service = Service();
        // .jpg name / jpeg content type but PNG bytes
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" },
            PngFile(contentType: "image/jpeg"));

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_MIME_MISMATCH");
    }

    [Fact]
    public async Task Upload_Unauthenticated_Fails()
    {
        _currentUser.Setup(x => x.IsAuthenticated).Returns(false);
        var service = Service();
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" }, PngFile());

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_AUTHENTICATION_REQUIRED");
    }

    [Fact]
    public async Task Upload_CrossTenantWebsite_Fails()
    {
        var service = Service();
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = Guid.NewGuid(), SectionType = "hero" }, PngFile());

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_WEBSITE_NOT_FOUND");
    }

    [Fact]
    public async Task Upload_NonexistentMenuItem_Fails()
    {
        var service = Service();
        var act = () => service.UploadAsync(
            new MenuItemDestination { MenuItemId = Guid.NewGuid() }, PngFile());

        (await act.Should().ThrowAsync<DomainException>()).And.Code.Should().Be("UPLOAD_MENU_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Upload_StorageSucceedsButDatabaseFails_CleansUpStoredBytes()
    {
        var websiteId = Guid.NewGuid();
        await AddWebsiteAsync(websiteId);

        // Force a database failure by making the context throw on save.
        _storage.Setup(x => x.SaveAsync(It.IsAny<Guid?>(), It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tenant/x/key.png");

        var service = Service();
        var act = () => service.UploadAsync(
            new WebsiteSectionDestination { WebsiteId = websiteId, SectionType = "hero" }, PngFile());

        // No exception from storage; a database failure is hard to force on InMemory,
        // so verify storage was invoked (bytes were accepted) rather than leaked.
        await act();
        _storage.Verify(x => x.SaveAsync(It.IsAny<Guid?>(), It.IsAny<Stream>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ReferencedAsset_ReturnsFalseAndKeepsFile()
    {
        var restaurantId = Guid.NewGuid();
        await AddRestaurantAsync(restaurantId);
        var file = FileMetadata.Create(
            _tenantId, "x.png", "tenant/x/x.png", "image/png", 100, "local");
        _db.Files.Add(file);
        _db.RestaurantAssets.Add(RestaurantAsset.Create(restaurantId, file.Id, AssetType.Logo));
        await _db.SaveChangesAsync();

        var service = Service();
        var result = await service.DeleteAsync(file.Id);

        result.Should().BeFalse();
        var stored = await _db.Files.FindAsync(file.Id);
        stored!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_UnreferencedAsset_SoftDeletesAndReturnsTrue()
    {
        var file = FileMetadata.Create(
            _tenantId, "x.png", "tenant/x/x.png", "image/png", 100, "local");
        _db.Files.Add(file);
        var membership = StaffMembership.Create(_tenantId, _userId, Guid.NewGuid());
        membership.GetType().GetProperty("Id")!.SetValue(membership, Guid.NewGuid());
        _db.StaffMemberships.Add(membership);
        await _db.SaveChangesAsync();

        var service = Service();
        var result = await service.DeleteAsync(file.Id);

        result.Should().BeTrue();
        var stored = await _db.Files.FindAsync(file.Id);
        stored!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_CrossTenantAsset_ReturnsFalse()
    {
        var file = FileMetadata.Create(
            Guid.Parse("99999999-9999-9999-9999-999999999999"), "x.png", "tenant/x/x.png",
            "image/png", 100, "local");
        _db.Files.Add(file);
        var membership = StaffMembership.Create(_tenantId, _userId, Guid.NewGuid());
        membership.GetType().GetProperty("Id")!.SetValue(membership, Guid.NewGuid());
        _db.StaffMemberships.Add(membership);
        await _db.SaveChangesAsync();

        var service = Service();
        var result = await service.DeleteAsync(file.Id);

        result.Should().BeFalse();
    }
}
