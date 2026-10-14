
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Api.Services.Authentication;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<User>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();
        services.AddOptions<EmailOptions>()
    .Bind(configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

        services.AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection(EmailVerificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<
            IEmailVerificationService,
            EmailVerificationService>();

        services.AddTransient<IEmailService, EmailService>();

        services.AddHttpContextAccessor();
        services.AddScoped<IAuthCookieService, AuthCookieService>();

        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<TenantContext>();

        services.AddScoped<ITenantContext>(
            sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<ITenantContextSetter>(
            sp => sp.GetRequiredService<TenantContext>());

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<ITenantResolver, AuthenticatedTenantResolver>();
        services.AddScoped<ITenantResolver, PublicTenantResolver>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<TenantAccessGuard>();
        services.AddScoped<IRestaurantService, RestaurantService>();

        return services;
    }
}