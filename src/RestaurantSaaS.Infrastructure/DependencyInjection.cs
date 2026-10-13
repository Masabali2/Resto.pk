using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.MenuCategories;
using RestaurantSaaS.Application.Features.MenuItems;
//using RestaurantSaaS.Application.Features.Menus;
using RestaurantSaaS.Application.Features.Restaurants;
using RestaurantSaaS.Application.Features.Staff;
using RestaurantSaaS.Application.Features.Tenants;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Infrastructure.Persistence;
using RestaurantSaaS.Infrastructure.Services;
using RestaurantSaaS.Infrastructure.Tenancy;
//using RestaurantSaaS.Infrastructure.Tenancy;

namespace RestaurantSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(
            sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(
            sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<ITenantResolver, AuthenticatedTenantResolver>();
        services.AddScoped<ITenantResolver, PublicTenantResolver>();
        services.AddScoped<TenantAccessGuard>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        //services.AddScoped<IStaffService, StaffService>();
        //services.AddScoped<ITenantService, TenantService>();
        //services.AddScoped<IMenuService, MenuService>();
        //services.AddScoped<IMenuCategoryService, MenuCategoryService>();
        //services.AddScoped<IMenuItemService, MenuItemService>();

        return services;
    }
}
