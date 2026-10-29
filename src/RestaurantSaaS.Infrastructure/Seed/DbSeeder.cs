
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Application.Features.Subscriptions;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.ValueObjects;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Seed;

public static class DbSeeder
{
    public const string DevPassword = "Masab@123";

    private const string PlatformAdminEmail =
        "alimasab923@gmail.com";

    private const string PermissionClaimType = "permission";

    private static readonly (string Name, string Description, bool IsPlatformRole)[] Roles =
    [
        (Permissions.TenantRoles.Owner, "Full tenant control", false),
        (Permissions.TenantRoles.Manager, "Day-to-day restaurant operations", false),
        (Permissions.TenantRoles.Kitchen, "Kitchen and order preparation", false),
        (Permissions.TenantRoles.Cashier, "Order and checkout operations", false),
        (Permissions.TenantRoles.Waiter, "Order placement and viewing", false),
        (Permissions.PlatformRoles.PlatformAdmin, "Platform administration", true)
    ];

    public static async Task SeedAsync(
        ApplicationDbContext db,
        IDateTimeProvider clock,
        IPasswordHasher<User>? passwordHasher = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);

        passwordHasher ??= new PasswordHasher<User>();

        await SeedRolesAsync(db);

        await SeedSubscriptionPlansAsync(db);

        await WebsiteTemplateCatalogSeeder.SeedAsync(db);

        var admin = await EnsurePlatformAdminUserAsync(
            db,
            clock,
            passwordHasher);

        var platformRoleId = await GetRoleIdAsync(
            db,
            Permissions.PlatformRoles.PlatformAdmin);

        await SeedPlatformPermissionClaimsAsync(
            db,
            platformRoleId);

        await EnsurePlatformAdminRoleAssignmentAsync(
            db,
            admin.Id,
            platformRoleId);
    }

    private static async Task SeedRolesAsync(
        ApplicationDbContext db)
    {
        foreach (var roleDefinition in Roles)
        {
            var normalizedName =
                roleDefinition.Name.ToUpperInvariant();

            var existingRole = await db.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    role => role.NormalizedName == normalizedName);

            if (existingRole is not null)
            {
                if (existingRole.IsPlatformRole !=
                    roleDefinition.IsPlatformRole)
                {
                    throw new InvalidOperationException(
                        $"Role '{roleDefinition.Name}' has an incompatible platform role setting.");
                }

                continue;
            }

            db.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                Name = roleDefinition.Name,
                NormalizedName = normalizedName,
                IsPlatformRole = roleDefinition.IsPlatformRole,
                Description = roleDefinition.Description
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the approved subscription plan catalogue.
    ///
    /// Each plan is matched by its name, so re-running the seeder never
    /// creates duplicates and never overwrites plans that platform
    /// administrators created or edited through the plan endpoints. Plans
    /// that are missing are inserted; plans that already exist are left
    /// untouched. Every price is a configured value in PKR — nothing is
    /// computed from another price, and no price is zero unless the plan is
    /// genuinely free (the free trial).
    /// </summary>
    private static async Task SeedSubscriptionPlansAsync(
        ApplicationDbContext db)
    {
        var existingNames = await db.SubscriptionPlans
            .IgnoreQueryFilters()
            .Select(plan => plan.Name)
            .ToHashSetAsync();

        var plans = new[]
        {
            // Free trial — genuinely free, no payment required.
            (
                Code: "free-trial",
                Name: SubscriptionTrialPolicy.TrialPlanName,
                Price: 0m,
                Interval: BillingInterval.Monthly,
                MaxStaff: 5,
                MaxMenuItems: 100,
                MaxOrdersPerMonth: 500,
                Description: "30-day free trial for a new Resto.pk workspace."
            ),
            // Starter — monthly and yearly variants share the stable code.
            (
                Code: "starter",
                Name: "Starter",
                Price: 1499m,
                Interval: BillingInterval.Monthly,
                MaxStaff: 3,
                MaxMenuItems: 50,
                MaxOrdersPerMonth: 500,
                Description: "Ideal for a single restaurant getting started."
            ),
            (
                Code: "starter",
                Name: "Starter Yearly",
                Price: 14990m,
                Interval: BillingInterval.Yearly,
                MaxStaff: 3,
                MaxMenuItems: 50,
                MaxOrdersPerMonth: 500,
                Description: "Ideal for a single restaurant getting started."
            ),
            // Professional.
            (
                Code: "professional",
                Name: "Professional",
                Price: 3999m,
                Interval: BillingInterval.Monthly,
                MaxStaff: 10,
                MaxMenuItems: 200,
                MaxOrdersPerMonth: 2000,
                Description: "For growing restaurants with multiple locations."
            ),
            (
                Code: "professional",
                Name: "Professional Yearly",
                Price: 39990m,
                Interval: BillingInterval.Yearly,
                MaxStaff: 10,
                MaxMenuItems: 200,
                MaxOrdersPerMonth: 2000,
                Description: "For growing restaurants with multiple locations."
            ),
            // Business.
            (
                Code: "business",
                Name: "Business",
                Price: 7999m,
                Interval: BillingInterval.Monthly,
                MaxStaff: 0,
                MaxMenuItems: 0,
                MaxOrdersPerMonth: 0,
                Description: "For established restaurant groups."
            ),
            (
                Code: "business",
                Name: "Business Yearly",
                Price: 79990m,
                Interval: BillingInterval.Yearly,
                MaxStaff: 0,
                MaxMenuItems: 0,
                MaxOrdersPerMonth: 0,
                Description: "For established restaurant groups."
            ),
        };

        foreach (var (code, name, price, interval, maxStaff,
                     maxMenuItems, maxOrdersPerMonth, description) in plans)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            db.SubscriptionPlans.Add(SubscriptionPlan.Create(
                code,
                name,
                Money.New(price, "PKR"),
                interval,
                maxStaff: maxStaff,
                maxMenuItems: maxMenuItems,
                maxOrdersPerMonth: maxOrdersPerMonth,
                description: description));
        }

        await db.SaveChangesAsync();
    }

    private static async Task<User> EnsurePlatformAdminUserAsync(
        ApplicationDbContext db,
        IDateTimeProvider clock,
        IPasswordHasher<User> passwordHasher)
    {
        var normalizedEmail = PlatformAdminEmail.ToUpperInvariant();

        var existingUser = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                user => user.NormalizedEmail == normalizedEmail);

        if (existingUser is not null)
        {
            return existingUser;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = PlatformAdminEmail,
            NormalizedUserName = normalizedEmail,
            Email = PlatformAdminEmail,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            CreatedAt = clock.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            DevPassword);

        db.Users.Add(user);

        await db.SaveChangesAsync();

        return user;
    }

    private static async Task<Guid> GetRoleIdAsync(
        ApplicationDbContext db,
        string roleName)
    {
        var normalizedName = roleName.ToUpperInvariant();

        var role = await db.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                existing => existing.NormalizedName == normalizedName);

        if (role is null)
        {
            throw new InvalidOperationException(
                $"Required role '{roleName}' was not found.");
        }

        return role.Id;
    }

    private static async Task SeedPlatformPermissionClaimsAsync(
        ApplicationDbContext db,
        Guid platformAdminRoleId)
    {
        var existingClaims = await db.RoleClaims
            .Where(claim => claim.RoleId == platformAdminRoleId)
            .ToListAsync();

        foreach (var permission in Permissions.AllPlatformPermissions)
        {
            var exists = existingClaims.Any(
                claim =>
                    claim.ClaimType == PermissionClaimType &&
                    claim.ClaimValue == permission);

            if (exists)
            {
                continue;
            }

            db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = platformAdminRoleId,
                ClaimType = PermissionClaimType,
                ClaimValue = permission
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsurePlatformAdminRoleAssignmentAsync(
        ApplicationDbContext db,
        Guid userId,
        Guid roleId)
    {
        var hasRole = await db.UserRoles.AnyAsync(
            userRole =>
                userRole.UserId == userId &&
                userRole.RoleId == roleId);

        if (hasRole)
        {
            return;
        }

        db.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = roleId
        });

        await db.SaveChangesAsync();
    }
}