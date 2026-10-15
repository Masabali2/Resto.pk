
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Api.Services.Authentication;
using RestaurantSaaS.Application.Common.Options;
using System.Reflection;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;
using RestaurantSaaS.Application.Features.Staff;
using RestaurantSaaS.Application.Common.Entitlements;
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

        // Centralized email notifications: producers enqueue durable outbox
        // messages; the background worker delivers them via IEmailService.
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddHostedService<EmailOutboxDeliveryWorker>();

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
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<TenantAccessGuard>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IRestaurantAssetService, RestaurantAssetService>();
        services.AddScoped<IWebsiteTemplateService, WebsiteTemplateService>();
        services.AddScoped<IWebsiteService, WebsiteService>();
        services.AddScoped<IPublicSiteService, PublicSiteService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IMenuCategoryService, MenuCategoryService>();
        services.AddScoped<IMenuItemService, MenuItemService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IEntitlementService, EntitlementService>();

        RegisterValidators(services);

        return services;
    }

    private static void RegisterValidators(IServiceCollection services)
    {
        var applicationAssembly = Assembly.Load("RestaurantSaaS.Application");

        services.AddValidatorsFromAssembly(applicationAssembly);
    }

}