
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
    /// Seeds the subscription plan that backs the 30-day free trial.
    ///
    /// Only the free trial plan is seeded. It carries a price of 0 (it is
    /// free by definition), and no paid plan is invented here — the paid
    /// catalogue is created by platform administrators through the existing
    /// plan endpoints, so the product never shows a price that was not
    /// deliberately configured.
    /// </summary>
    private static async Task SeedSubscriptionPlansAsync(
        ApplicationDbContext db)
    {
        var existing = await db.SubscriptionPlans
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                plan => plan.Name == SubscriptionTrialPolicy.TrialPlanName);

        if (existing is not null)
        {
            return;
        }

        db.SubscriptionPlans.Add(SubscriptionPlan.Create(
            SubscriptionTrialPolicy.TrialPlanName,
            Money.Zero(),
            BillingInterval.Monthly,
            maxStaff: 5,
            maxMenuItems: 100,
            maxOrdersPerMonth: 500,
            description: "30-day free trial for a new Resto.pk workspace."));

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