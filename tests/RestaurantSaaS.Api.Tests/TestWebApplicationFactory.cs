using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Api.Tests;

/// <summary>
/// Test host for HTTP integration tests.
///
/// Sets required environment variables so Program.cs can start without
/// user-secrets, then overrides the DbContext to use InMemory with a unique
/// database name per fixture to avoid cross-test contamination.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    public TestWebApplicationFactory()
    {
        _databaseName = $"resto-test-{Guid.NewGuid():N}";

        // Set required environment variables before the host builds
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ShopperSession__Secret", "this-is-a-test-secret-key-that-is-at-least-32-chars-long!");
        Environment.SetEnvironmentVariable("ShopperSession__LifetimeHours", "24");
        Environment.SetEnvironmentVariable("ShopperSession__SameSite", "Lax");
        Environment.SetEnvironmentVariable("ShopperSession__Secure", "false");
        Environment.SetEnvironmentVariable("ShopperSession__AllowedOrigins", "https://localhost:4200");
        Environment.SetEnvironmentVariable("Jwt__Secret", "this-is-a-test-jwt-secret-key-that-is-at-least-32-chars!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://localhost:5001");
        Environment.SetEnvironmentVariable("Jwt__Audience", "resto.pk");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpirationDays", "7");
        Environment.SetEnvironmentVariable("Email__SmtpHost", "localhost");
        Environment.SetEnvironmentVariable("Email__SmtpPort", "25");
        Environment.SetEnvironmentVariable("Email__SmtpUser", "test@resto.pk");
        Environment.SetEnvironmentVariable("Email__SmtpPassword", "test-password");
        Environment.SetEnvironmentVariable("Email__FromEmail", "test@resto.pk");
        Environment.SetEnvironmentVariable("Email__FromName", "Test");
        Environment.SetEnvironmentVariable("EmailVerification__HmacKey", "this-is-a-test-email-verification-hmac-key-32-chars!!");
        Environment.SetEnvironmentVariable("EmailVerification__ExpirationHours", "24");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext with InMemory for testing
            // Remove every ApplicationDbContext-related registration so
            // Program.cs's TryAdd won't leave a duplicate provider.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                    || d.ServiceType == typeof(ApplicationDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public string DatabaseName => _databaseName;
}
