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
/// user-secrets, then overrides the DbContext to use InMemory.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestWebApplicationFactory()
    {
        // Set required environment variables before the host builds
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
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("RestoTest"));
        });
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
