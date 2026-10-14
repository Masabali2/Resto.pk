
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Api.Services.Authentication;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Common.Options;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Websites;
using RestaurantSaaS.Application.Features.Tenants;
using System.Reflection;
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
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<TenantAccessGuard>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IRestaurantAssetService, RestaurantAssetService>();
        services.AddScoped<IWebsiteTemplateService, WebsiteTemplateService>();
        services.AddScoped<IWebsiteService, WebsiteService>();
        services.AddScoped<IPublicSiteService, PublicSiteService>();

        RegisterValidators(services);

        return services;
    }

    private static void RegisterValidators(IServiceCollection services)
    {
        var validatorOpenType = typeof(IValidator<>);
        var seen = new HashSet<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                foreach (var serviceType in type.GetInterfaces()
                             .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorOpenType))
                {
                    if (seen.Add(serviceType))
                    {
                        services.AddScoped(serviceType, type);
                    }
                }
            }
        }
    }
}