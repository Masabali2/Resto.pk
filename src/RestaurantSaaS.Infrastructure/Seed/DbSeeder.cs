using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Authorization;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;

namespace RestaurantSaaS.Infrastructure.Seed;

/// <summary>
/// Development-only seed foundation (requirement #36). Idempotent — safe to run
/// on every startup in Development. NEVER invoked in Production (the API host
/// guards the call). All credentials are clearly-marked dev placeholders using
/// the reserved example.com domain; no real secrets exist in this file.
///
/// Seeding uses DbContext directly (no UserManager) so it works before any DI
/// container exists and inside tests. Password hashing still goes through
/// ASP.NET Core Identity's PBKDF2 hasher — never a hand-rolled hash.
/// </summary>
public static class DbSeeder
{
    /// <summary>DEV-ONLY password. Not a production credential.</summary>
    public const string DevPassword = "Dev-Only_P@ssw0rd!";

    public static async Task SeedAsync(ApplicationDbContext db, IDateTimeProvider clock)
    {
        await SeedRolesAsync(db);
        await SeedPlatformAdminAsync(db, clock);
        await SeedDemoTenantsAsync(db, clock);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext db)
    {
        // Tenant-level role DEFINITIONS live in AspNetRoles but are attached to
        // users exclusively through StaffMemberships — never via AspNetUserRoles.
        var tenantRoles = new[]
        {
            (Permissions.TenantRoles.Owner,   "Full tenant control"),
            (Permissions.TenantRoles.Manager, "Day-to-day operations"),
            (Permissions.TenantRoles.Kitchen, "Order preparation view/transitions"),
            (Permissions.TenantRoles.Cashier, "Orders & checkout"),
            (Permissions.TenantRoles.Waiter,  "Order placement/view")
        };

        foreach (var (name, description) in tenantRoles)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
            {
                db.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    IsPlatformRole = false,
                    Description = description
                });
            }
        }

        // Global platform role — assigned through AspNetUserRoles (AddToRoleAsync path).
        if (!await db.Roles.AnyAsync(r => r.Name == Permissions.PlatformRoles.PlatformAdmin))
        {
            db.Roles.Add(new Role
            {
                Id = Guid.NewGuid(),
                Name = Permissions.PlatformRoles.PlatformAdmin,
                NormalizedName = Permissions.PlatformRoles.PlatformAdmin.ToUpperInvariant(),
                IsPlatformRole = true,
                Description = "Platform operator - cross-tenant administration"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedPlatformAdminAsync(ApplicationDbContext db, IDateTimeProvider clock)
    {
        const string email = "dev.platformadmin@example.com";

        var admin = await EnsureUserAsync(db, email, clock);

        var platformRoleId = (await db.Roles.FirstAsync(r => r.Name == Permissions.PlatformRoles.PlatformAdmin)).Id;

        var hasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == platformRoleId);
        if (!hasRole)
        {
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = platformRoleId });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoTenantsAsync(ApplicationDbContext db, IDateTimeProvider clock)
    {
        var specs = new (string TenantName, string Slug, string OwnerEmail)[]
        {
            ("Dev Pizza House",  "dev-pizza-house",  "dev.owner.a@example.com"),
            ("Dev Burger Joint", "dev-burger-joint", "dev.owner.b@example.com"),
        };

        Guid? tenantA = null, tenantB = null;

        foreach (var (spec, i) in specs.Select((s, i) => (s, i)))
        {
            var (tenantName, slug, ownerEmail) = spec;

            var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant is null)
            {
                tenant = Domain.Entities.Tenants.Tenant.Create(tenantName, slug);
                tenant.ChangeStatus(TenantStatus.Active); // dev tenants usable immediately
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
            }

            var owner = await EnsureUserAsync(db, ownerEmail, clock);
            await EnsureMembershipAsync(db, tenant.Id, owner.Id, Permissions.TenantRoles.Owner);

            if (i == 0) tenantA = tenant.Id; else tenantB = tenant.Id;
        }

        // Multi-tenant membership matrix (requirement #4/#36):
        var manager1 = await EnsureUserAsync(db, "dev.manager1@example.com", clock);
        var kitchenB = await EnsureUserAsync(db, "dev.kitchen.b@example.com", clock);
        var multi    = await EnsureUserAsync(db, "dev.multitenant@example.com", clock);

        await EnsureMembershipAsync(db, tenantA!.Value, manager1.Id, Permissions.TenantRoles.Manager);
        await EnsureMembershipAsync(db, tenantB!.Value, kitchenB.Id, Permissions.TenantRoles.Kitchen);
        await EnsureMembershipAsync(db, tenantA.Value, multi.Id, Permissions.TenantRoles.Manager); // Manager @ A
        await EnsureMembershipAsync(db, tenantB.Value, multi.Id, Permissions.TenantRoles.Kitchen);  // Kitchen @ B

        // One restaurant per demo tenant (Tenant 1->M Restaurants exercised by tests).
        var restaurants = new[] { (tenantA.Value, "Pizza House Daska"), (tenantB.Value, "Burger Joint Gulberg") };
        foreach (var (tid, name) in restaurants)
        {
            if (!await db.Restaurants.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tid))
            {
                db.Restaurants.Add(Domain.Entities.Restaurants.Restaurant.Create(
                    tid, name,
                    Domain.ValueObjects.Address.Create("1 Main Bazaar", "Daska", "Pakistan")));
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<User> EnsureUserAsync(ApplicationDbContext db, string email, IDateTimeProvider clock)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        if (user is not null) return user;

        user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true, // DEV convenience only — production flow verifies email
            CreatedAt = clock.UtcNow
        };

        // Identity's own PBKDF2 hasher — same algorithm as runtime registration.
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, DevPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task EnsureMembershipAsync(ApplicationDbContext db, Guid tenantId, Guid userId, string roleName)
    {
        var roleId = (await db.Roles.FirstAsync(r => r.Name == roleName)).Id;

        var exists = await db.StaffMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.UserId == userId);
        if (exists) return;

        db.StaffMemberships.Add(Domain.Entities.Tenants.StaffMembership.Create(tenantId, userId, roleId));
        await db.SaveChangesAsync();
    }
}
