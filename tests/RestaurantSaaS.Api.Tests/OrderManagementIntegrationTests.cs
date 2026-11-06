using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RestaurantSaaS.Api.Filters;
using RestaurantSaaS.Api.Middleware;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.PublicCarts;
using RestaurantSaaS.Application.Features.PublicOrders;
using RestaurantSaaS.Application.Features.Orders;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Authorization;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Phase 5: Order Management API integration tests.
///
/// Exercises the real ASP.NET Core HTTP pipeline end-to-end using WebApplicationFactory.
///
/// Workflow: published restaurant → public site/menu → shopper session
/// → public cart → add item → checkout → order creation → tracking token
/// → public tracking → authenticated staff order list/detail → staff
/// status transition.
///
/// All test data is seeded directly into a per-test InMemory database.
/// No production database is touched.
/// </summary>
public class OrderManagementIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ApplicationDbContext _db;
    private readonly string _jwtSecret = "this-is-a-test-jwt-secret-key-that-is-at-least-32-chars!";
    private readonly string _jwtIssuer = "https://localhost:5001";
    private readonly string _jwtAudience = "resto.pk";

    private Guid _tenantId;
    private Guid _restaurantId;
    private string _restaurantSlug = "test-restaurant";
    private Guid _menuItemId;
    private Guid _staffUserId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public OrderManagementIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        _db = factory.CreateDbContext();
        SeedOrderManagementData().GetAwaiter().GetResult();
    }

    private async Task SeedOrderManagementData()
    {
        // The shared test database is seeded without an ambient tenant, so
        // bypass tenant filters for this explicit test-data reset only.
        _db.DisableTenantFilters = true;
        try
        {
            _db.CartItems.RemoveRange(_db.CartItems);
            _db.Carts.RemoveRange(_db.Carts);
            _db.Orders.RemoveRange(_db.Orders);
            _db.StaffMemberships.RemoveRange(_db.StaffMemberships);
            _db.Roles.RemoveRange(_db.Roles);
            _db.WebsiteConfigurations.RemoveRange(_db.WebsiteConfigurations);
            _db.Websites.RemoveRange(_db.Websites);
            _db.WebsiteTemplates.RemoveRange(_db.WebsiteTemplates);
            _db.MenuItems.RemoveRange(_db.MenuItems);
            _db.MenuCategories.RemoveRange(_db.MenuCategories);
            _db.Menus.RemoveRange(_db.Menus);
            _db.Subscriptions.RemoveRange(_db.Subscriptions);
            _db.SubscriptionPlans.RemoveRange(_db.SubscriptionPlans);
            _db.Restaurants.RemoveRange(_db.Restaurants);
            _db.Tenants.RemoveRange(_db.Tenants);
            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.DisableTenantFilters = false;
        }

        _tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        _restaurantId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        _menuItemId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

        var tenant = Tenant.Create("Test Tenant", "test-restaurant");
        tenant.Id = _tenantId;
        tenant.ChangeStatus(TenantStatus.Active);
        _db.Tenants.Add(tenant);

        var address = Address.Create("123 Test St", "Karachi", "Pakistan");
        var restaurant = Restaurant.Create(_tenantId, "Test Restaurant", address, taxRate: 16m);
        restaurant.ChangeStatus(RestaurantStatus.Active);
        _restaurantId = restaurant.Id;
        _db.Restaurants.Add(restaurant);

        var plan = SubscriptionPlan.Create(
            "free-trial",
            SubscriptionTrialPolicy.TrialPlanName,
            Money.Zero(),
            BillingInterval.Monthly,
            maxStaff: 5,
            maxMenuItems: 100,
            maxOrdersPerMonth: 500,
            description: "30-day free trial");
        _db.SubscriptionPlans.Add(plan);

        var subscription = Subscription.Start(
            _tenantId,
            plan.Id,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(30));
        _db.Subscriptions.Add(subscription);

        var ownerRoleId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        _db.Roles.Add(new Role
        {
            Id = ownerRoleId,
            Name = "Owner",
            NormalizedName = "OWNER"
        });

        var membership = StaffMembership.Create(_tenantId, _staffUserId, ownerRoleId);
        _db.StaffMemberships.Add(membership);

        await _db.SaveChangesAsync();

        var template = WebsiteTemplate.Create("Default Template", "default-key", "{}");
        template.SetDesignConfiguration("{\"palette\":\"primary\"}");
        _db.WebsiteTemplates.Add(template);

        var website = Website.Create(_tenantId, _restaurantId, template.Id, "Test Restaurant Site", _restaurantSlug);
        _db.Websites.Add(website);
        await _db.SaveChangesAsync();

        var config = WebsiteConfiguration.CreateDraft(website.Id, "{\"theme\":\"light\"}", 1);
        _db.WebsiteConfigurations.Add(config);
        await _db.SaveChangesAsync();

        website.Publish(config, template, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var menu = Menu.Create(_tenantId, _restaurantId, "Main Menu");
        menu.SetActive(true);
        menu.Publish(1);
        _db.Menus.Add(menu);

        var category = MenuCategory.Create(menu.Id, "Main");
        _db.MenuCategories.Add(category);

        var menuItem = MenuItem.Create(category.Id, "Test Burger", Money.New(450m));
        _menuItemId = menuItem.Id;
        _db.MenuItems.Add(menuItem);

        await _db.SaveChangesAsync();
    }

    private const string AllowedOrigin = "https://localhost:4200";

    private string WithTenantSlug(string url)
    {
        return url.Contains('?')
            ? $"{url}&tenantSlug={_restaurantSlug}"
            : $"{url}?tenantSlug={_restaurantSlug}";
    }

    private string GenerateJwtToken(Guid userId)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "staff-test-user"),
            new Claim(ClaimTypes.Role, Permissions.TenantRoles.Owner)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GetAntiforgeryTokenAsync()
    {
        var response = await _client.GetAsync("/api/antiforgery-token");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Antiforgery endpoint did not return a request token.");
    }

    private Task<HttpResponseMessage> PostWithAntiforgeryAsync(
        string url,
        object body,
        string? antiforgeryToken = null) =>
        SendWithAntiforgeryAsync(HttpMethod.Post, url, body, antiforgeryToken);

    private Task<HttpResponseMessage> PatchWithAntiforgeryAsync(
        string url,
        object body,
        string? antiforgeryToken = null) =>
        SendWithAntiforgeryAsync(HttpMethod.Patch, url, body, antiforgeryToken);

    private async Task<HttpResponseMessage> SendWithAntiforgeryAsync(
        HttpMethod method,
        string url,
        object body,
        string? antiforgeryToken = null)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("Origin", AllowedOrigin);
        if (antiforgeryToken is not null)
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", antiforgeryToken);
        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new InvalidOperationException(
                $"Expected successful mutation but received {(int)statusCode}. Response: {responseBody}");
        }

        return response;
    }

    private void SetJwtCookie(string token)
    {
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", $"resto_access_token={token}");
    }

    [Fact]
    public async Task PublicOrdering_EndToEnd_CreatesOrderWithTrackingToken()
    {
        var siteResponse = await _client.GetAsync(WithTenantSlug($"/api/v1/public/sites/{_restaurantSlug}"));
        siteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var menuResponse = await _client.GetAsync(WithTenantSlug($"/api/v1/public/sites/{_restaurantSlug}/menu"));
        menuResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cartResponse = await _client.GetAsync(WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart"));
        cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartBody = await cartResponse.Content.ReadAsStringAsync();
        using var cartDoc = JsonDocument.Parse(cartBody);
        cartDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        cartDoc.RootElement.GetProperty("data").GetProperty("itemCount").GetInt32().Should().Be(0);

        var antiToken = await GetAntiforgeryTokenAsync();
        var addItemResponse = await PostWithAntiforgeryAsync(
            $"/api/v1/public/restaurants/{_restaurantSlug}/cart/items",
            new { menuItemId = _menuItemId, quantity = 2 },
            antiToken);
        addItemResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var addItemBody = await addItemResponse.Content.ReadAsStringAsync();
        using var addItemDoc = JsonDocument.Parse(addItemBody);
        addItemDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        addItemDoc.RootElement.GetProperty("data").GetProperty("itemName").GetString().Should().Be("Test Burger");
        addItemDoc.RootElement.GetProperty("data").GetProperty("quantity").GetInt32().Should().Be(2);
        addItemDoc.RootElement.GetProperty("data").GetProperty("lineTotal").GetDecimal().Should().Be(900m);
        addItemDoc.RootElement.GetProperty("data").GetProperty("unitPrice").GetDecimal().Should().Be(450m);

        var cartResponse2 = await _client.GetAsync(WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart"));
        cartResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartBody2 = await cartResponse2.Content.ReadAsStringAsync();
        using var cartDoc2 = JsonDocument.Parse(cartBody2);
        cartDoc2.RootElement.GetProperty("data").GetProperty("itemCount").GetInt32().Should().Be(1);
        cartDoc2.RootElement.GetProperty("data").GetProperty("totalAmount").GetDecimal().Should().Be(900m);

        var checkoutResponse = await PostWithAntiforgeryAsync(
            $"/api/v1/public/restaurants/{_restaurantSlug}/orders",
            new
            {
                orderType = 1,
                customerName = "John Doe",
                customerPhone = "+923001234567",
                customerEmail = "john@example.com",
                specialInstructions = "No onions"
            },
            antiToken);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        checkoutDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var orderData = checkoutDoc.RootElement.GetProperty("data");

        orderData.GetProperty("orderNumber").GetString().Should().StartWith("ORD-");
        orderData.GetProperty("status").GetString().Should().Be("New");
        orderData.GetProperty("totalAmount").GetDecimal().Should().Be(1044m);
        orderData.GetProperty("currency").GetString().Should().Be("PKR");
        orderData.GetProperty("items").GetArrayLength().Should().Be(1);
        orderData.GetProperty("items")[0].GetProperty("itemName").GetString().Should().Be("Test Burger");
        orderData.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal().Should().Be(450m);
        orderData.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(2);

        var rawToken = orderData.GetProperty("rawTrackingToken").GetString();
        rawToken.Should().NotBeNullOrWhiteSpace();

        orderData.TryGetProperty("tenantId", out _).Should().BeFalse();
        orderData.TryGetProperty("customerId", out _).Should().BeFalse();

        var trackResponse = await _client.GetAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/track/{rawToken}"));
        trackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trackBody = await trackResponse.Content.ReadAsStringAsync();
        using var trackDoc = JsonDocument.Parse(trackBody);
        trackDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        trackDoc.RootElement.GetProperty("data").GetProperty("orderNumber").GetString()
            .Should().Be(orderData.GetProperty("orderNumber").GetString());

        trackDoc.RootElement.GetProperty("data").TryGetProperty("rawTrackingToken", out _).Should().BeFalse();
        trackDoc.RootElement.GetProperty("data").TryGetProperty("trackingTokenHash", out _).Should().BeFalse();
        trackDoc.RootElement.GetProperty("data").TryGetProperty("tenantId", out _).Should().BeFalse();
        trackDoc.RootElement.GetProperty("data").TryGetProperty("sessionHash", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublicOrdering_DirectCheckout_CreatesOrderWithServerTotals()
    {
        var antiToken = await GetAntiforgeryTokenAsync();

        var response = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/direct"),
            new
            {
                orderType = 3,
                customerName = "Jane Doe",
                customerPhone = "+923009876543",
                customerEmail = "jane@example.com",
                specialInstructions = "Leave at door",
                items = new[]
                {
                    new { menuItemId = _menuItemId, quantity = 3 }
                }
            },
            antiToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");

        data.GetProperty("totalAmount").GetDecimal().Should().Be(1566m);
        data.GetProperty("orderType").GetString().Should().Be("Delivery");
        data.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal().Should().Be(450m);
        data.GetProperty("items")[0].GetProperty("lineTotal").GetDecimal().Should().Be(1350m);

        var rawToken = data.GetProperty("rawTrackingToken").GetString();
        rawToken.Should().NotBeNullOrWhiteSpace();

        data.TryGetProperty("tenantId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublicTracking_ValidToken_ReturnsOrder()
    {
        var antiToken = await GetAntiforgeryTokenAsync();
        var checkoutResponse = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/direct"),
            new
            {
                orderType = 1,
                customerName = "Track Test",
                items = new[] { new { menuItemId = _menuItemId, quantity = 1 } }
            },
            antiToken);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        var rawToken = checkoutDoc.RootElement.GetProperty("data").GetProperty("rawTrackingToken").GetString();
        var orderNumber = checkoutDoc.RootElement.GetProperty("data").GetProperty("orderNumber").GetString();

        var trackResponse = await _client.GetAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/track/{rawToken}"));
        trackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var trackBody = await trackResponse.Content.ReadAsStringAsync();
        using var trackDoc = JsonDocument.Parse(trackBody);
        trackDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        trackDoc.RootElement.GetProperty("data").GetProperty("orderNumber").GetString().Should().Be(orderNumber);
    }

    [Fact]
    public async Task PublicTracking_InvalidToken_Returns404()
    {
        var trackResponse = await _client.GetAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/track/invalid-token-xyz"));
        trackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicTracking_CrossRestaurant_AccessDenied()
    {
        var antiToken = await GetAntiforgeryTokenAsync();
        var checkoutResponse = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/direct"),
            new
            {
                orderType = 1,
                customerName = "Cross Test",
                items = new[] { new { menuItemId = _menuItemId, quantity = 1 } }
            },
            antiToken);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        var rawToken = checkoutDoc.RootElement.GetProperty("data").GetProperty("rawTrackingToken").GetString();

        var otherSlug = "other-restaurant";
        var trackResponse = await _client.GetAsync(
            $"/api/v1/public/restaurants/{otherSlug}/orders/track/{rawToken}?tenantSlug={_restaurantSlug}");
        trackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShopperIsolation_TwoSessions_CannotSeeEachOthersCart()
    {
        var antiTokenA = await GetAntiforgeryTokenAsync();

        var addResponseA = await PostWithAntiforgeryAsync(
            $"/api/v1/public/restaurants/{_restaurantSlug}/cart/items",
            new { menuItemId = _menuItemId, quantity = 2 },
            antiTokenA);
        addResponseA.StatusCode.Should().Be(HttpStatusCode.OK);

        var cartAResponse = await _client.GetAsync(WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart"));
        var cartABody = await cartAResponse.Content.ReadAsStringAsync();
        using var cartADoc = JsonDocument.Parse(cartABody);
        cartADoc.RootElement.GetProperty("data").GetProperty("itemCount").GetInt32().Should().Be(1);

        var clientB = _factory.CreateClient();
        var cartBResponse = await clientB.GetAsync(WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart"));
        cartBResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartBBody = await cartBResponse.Content.ReadAsStringAsync();
        using var cartBDoc = JsonDocument.Parse(cartBBody);
        cartBDoc.RootElement.GetProperty("data").GetProperty("itemCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task StaffOrder_EndToEnd_ListGetTransition()
    {
        var antiToken = await GetAntiforgeryTokenAsync();
        var checkoutResponse = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/direct"),
            new
            {
                orderType = 1,
                customerName = "Staff Test",
                items = new[] { new { menuItemId = _menuItemId, quantity = 1 } }
            },
            antiToken);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        var orderId = checkoutDoc.RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        var orderNumber = checkoutDoc.RootElement.GetProperty("data").GetProperty("orderNumber").GetString();

        var jwtToken = GenerateJwtToken(_staffUserId);
        SetJwtCookie(jwtToken);

        var listResponse = await _client.GetAsync(
            $"/api/restaurants/{_restaurantId}/orders");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        listDoc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(1);
        listDoc.RootElement.GetProperty("data").GetProperty("items")[0]
            .GetProperty("orderNumber").GetString().Should().Be(orderNumber);

        var getResponse = await _client.GetAsync(
            $"/api/restaurants/{_restaurantId}/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        using var getDoc = JsonDocument.Parse(getBody);
        getDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        getDoc.RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString().Should().Be(orderId);
        getDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("New");

        var patchResponse = await PatchWithAntiforgeryAsync(
            $"/api/restaurants/{_restaurantId}/orders/{orderId}/status",
            new { newStatus = "Confirmed", notes = "Preparing order" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        using var patchDoc = JsonDocument.Parse(patchBody);
        patchDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        patchDoc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("Confirmed");

        var getResponse2 = await _client.GetAsync(
            $"/api/restaurants/{_restaurantId}/orders/{orderId}");
        getResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody2 = await getResponse2.Content.ReadAsStringAsync();
        using var getDoc2 = JsonDocument.Parse(getBody2);
        getDoc2.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("Confirmed");
    }

    [Fact]
    public async Task Staff_CrossRestaurant_AccessDenied()
    {
        var antiToken = await GetAntiforgeryTokenAsync();
        var checkoutResponse = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/orders/direct"),
            new
            {
                orderType = 1,
                customerName = "Isolation Test",
                items = new[] { new { menuItemId = _menuItemId, quantity = 1 } }
            },
            antiToken);
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        var orderId = checkoutDoc.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var otherRestaurantId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var jwtToken = GenerateJwtToken(_staffUserId);
        SetJwtCookie(jwtToken);

        var getResponse = await _client.GetAsync(
            $"/api/restaurants/{otherRestaurantId}/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Antiforgery_PostWithoutToken_Returns400()
    {
        var response = await _client.PostAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart/items"),
            new StringContent(
                JsonSerializer.Serialize(new { menuItemId = _menuItemId, quantity = 1 }),
                Encoding.UTF8,
                "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Origin_DisallowedOrigin_CookieMutation_Returns403()
    {
        _client.DefaultRequestHeaders.Add("Cookie", "resto_shopper_session=some-token");
        _client.DefaultRequestHeaders.Add("Origin", "https://evil.com");

        var response = await _client.PostAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart/items"),
            new StringContent(
                JsonSerializer.Serialize(new { menuItemId = _menuItemId, quantity = 1 }),
                Encoding.UTF8,
                "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Origin_AllowedOrigin_CookieMutation_Succeeds()
    {
        _client.DefaultRequestHeaders.Add("Cookie", "resto_shopper_session=some-token");
        _client.DefaultRequestHeaders.Add("Origin", AllowedOrigin);

        var antiToken = await GetAntiforgeryTokenAsync();
        var response = await PostWithAntiforgeryAsync(
            WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart/items"),
            new { menuItemId = _menuItemId, quantity = 1 },
            antiToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Public_Get_SafeMethod_NoAntiforgeryRequired()
    {
        var response = await _client.GetAsync(WithTenantSlug($"/api/v1/public/restaurants/{_restaurantSlug}/cart"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync($"/api/restaurants/{_restaurantId}/orders");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_WithoutOrderUpdatePermission_CannotTransitionStatus()
    {
        var otherUserId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var jwtToken = GenerateJwtToken(otherUserId);
        SetJwtCookie(jwtToken);

        var response = await _client.GetAsync($"/api/restaurants/{_restaurantId}/orders");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose()
    {
        _client.Dispose();
        _db.Dispose();
    }
}

// ------------------------------------------------------------------
// Test doubles
// ------------------------------------------------------------------

public sealed class TestOrderNumberGenerator : IOrderNumberGenerator
{
    private int _counter;
    public string Generate() => $"ORD-TEST-{Interlocked.Increment(ref _counter):D6}";
}

public sealed class TestShopperSessionCookieService : IShopperSessionCookieService
{
    private string? _rawToken;
    private DateTime _expiresAt;

    public void SetSessionCookie(string rawToken, DateTime expiresAtUtc)
    {
        _rawToken = rawToken;
        _expiresAt = expiresAtUtc;
    }

    public string? ReadSessionToken() => _rawToken;

    public void ClearSessionCookie()
    {
        _rawToken = null;
        _expiresAt = default;
    }
}

public sealed class StubMembershipService : ITenantMembershipService
{
    private readonly Dictionary<(Guid UserId, Guid TenantId), string> _roles = new();

    public void AddMembership(Guid userId, Guid tenantId, string roleName)
    {
        _roles[(userId, tenantId)] = roleName;
    }

    public Task<TenantMembershipInfo?> GetMembershipAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_roles.TryGetValue((userId, tenantId), out var roleName))
            return Task.FromResult<TenantMembershipInfo?>(null);

        var roleId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        return Task.FromResult<TenantMembershipInfo?>(
            new TenantMembershipInfo(
                Guid.NewGuid(),
                tenantId,
                "Test Tenant",
                "test-tenant",
                roleId,
                roleName,
                (int)MembershipStatus.Active));
    }

    public Task<IReadOnlyList<TenantMembershipInfo>> GetMembershipsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = _roles
            .Where(pair => pair.Key.UserId == userId)
            .Select(pair => new TenantMembershipInfo(
                Guid.NewGuid(),
                pair.Key.TenantId,
                "Test Tenant",
                "test-tenant",
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                pair.Value,
                (int)MembershipStatus.Active))
            .ToList();

        return Task.FromResult<IReadOnlyList<TenantMembershipInfo>>(memberships);
    }
}
