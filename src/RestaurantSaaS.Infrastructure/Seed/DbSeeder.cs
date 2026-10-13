
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using RestaurantSaaS.Application.Common.Authorization;
//using RestaurantSaaS.Application.Common.Interfaces;
//using RestaurantSaaS.Domain.Entities.Identity;
//using RestaurantSaaS.Domain.Enums;
//using RestaurantSaaS.Infrastructure.Persistence;

//namespace RestaurantSaaS.Infrastructure.Seed;

///// <summary>
///// Development-only database seeder.
/////
///// Creates platform roles, a platform administrator, two demo tenants,
///// tenant staff memberships, and one restaurant per demo tenant.
/////
///// Idempotent for existing records: repeated runs do not create duplicates.
///// Must only be invoked by the API host in Development.
///// </summary>
//public static class DbSeeder
//{
//    /// <summary>
//    /// Development-only placeholder password.
//    /// Never use this credential in production.
//    /// </summary>
//    public const string DevPassword = "Dev-Only_P@ssw0rd!";

//    private const string PlatformAdminEmail =
//        "dev.platformadmin@example.com";

//    private static readonly (string Name, string Description)[] TenantRoles =
//    [
//        (Permissions.TenantRoles.Owner, "Full tenant control"),
//        (Permissions.TenantRoles.Manager, "Day-to-day operations"),
//        (Permissions.TenantRoles.Kitchen, "Order preparation view and transitions"),
//        (Permissions.TenantRoles.Cashier, "Orders and checkout"),
//        (Permissions.TenantRoles.Waiter, "Order placement and viewing")
//    ];

//    private static readonly (
//        string TenantName,
//        string Slug,
//        string OwnerEmail,
//        string RestaurantName,
//        string AddressLine1,
//        string City
//    )[] DemoTenants =
//    [
//        (
//            "Dev Pizza House",
//            "dev-pizza-house",
//            "dev.owner.a@example.com",
//            "Pizza House Daska",
//            "1 Main Bazaar",
//            "Daska"
//        ),
//        (
//            "Dev Burger Joint",
//            "dev-burger-joint",
//            "dev.owner.b@example.com",
//            "Burger Joint Gulberg",
//            "1 Main Boulevard",
//            "Lahore"
//        )
//    ];

//    /// <summary>
//    /// Seeds development data.
//    ///
//    /// The optional password hasher allows the application's configured
//    /// Identity hasher to be supplied without breaking existing calls.
//    /// </summary>
//    public static async Task SeedAsync(
//        ApplicationDbContext db,
//        IDateTimeProvider clock,
//        IPasswordHasher<User>? passwordHasher = null)
//    {
//        ArgumentNullException.ThrowIfNull(db);
//        ArgumentNullException.ThrowIfNull(clock);

//        passwordHasher ??= new PasswordHasher<User>();

//        await SeedRolesAsync(db);

//        await SeedPlatformAdminAsync(
//            db,
//            clock,
//            passwordHasher);

//        var tenantIds = new Guid[DemoTenants.Length];

//        for (var i = 0; i < DemoTenants.Length; i++)
//        {
//            var spec = DemoTenants[i];

//            tenantIds[i] = await EnsureDemoTenantAsync(
//                db,
//                spec.TenantName,
//                spec.Slug);

//            var owner = await EnsureUserAsync(
//                db,
//                spec.OwnerEmail,
//                clock,
//                passwordHasher);

//            await EnsureMembershipAsync(
//                db,
//                tenantIds[i],
//                owner.Id,
//                Permissions.TenantRoles.Owner);
//        }

//        await SeedMembershipMatrixAsync(
//            db,
//            clock,
//            passwordHasher,
//            tenantIds[0],
//            tenantIds[1]);

//        await SeedRestaurantsAsync(db, tenantIds);
//    }

//    // ------------------------------------------------------------
//    // Roles
//    // ------------------------------------------------------------

//    private static async Task SeedRolesAsync(
//        ApplicationDbContext db)
//    {
//        foreach (var (name, description) in TenantRoles)
//        {
//            await EnsureRoleAsync(
//                db,
//                name,
//                description,
//                isPlatformRole: false);
//        }

//        await EnsureRoleAsync(
//            db,
//            Permissions.PlatformRoles.PlatformAdmin,
//            "Platform operator - cross-tenant administration",
//            isPlatformRole: true);

//        await db.SaveChangesAsync();
//    }

//    private static async Task EnsureRoleAsync(
//        ApplicationDbContext db,
//        string roleName,
//        string description,
//        bool isPlatformRole)
//    {
//        var normalizedName = roleName.ToUpperInvariant();

//        var existingRole = await db.Roles
//            .IgnoreQueryFilters()
//            .FirstOrDefaultAsync(
//                role => role.NormalizedName == normalizedName);

//        if (existingRole is not null)
//        {
//            if (existingRole.IsPlatformRole != isPlatformRole)
//            {
//                throw new InvalidOperationException(
//                    $"Role '{roleName}' already exists with an " +
//                    "incompatible IsPlatformRole value.");
//            }

//            return;
//        }

//        db.Roles.Add(new Role
//        {
//            Id = Guid.NewGuid(),
//            Name = roleName,
//            NormalizedName = normalizedName,
//            IsPlatformRole = isPlatformRole,
//            Description = description
//        });
//    }

//    // ------------------------------------------------------------
//    // Platform administrator
//    // ------------------------------------------------------------

//    private static async Task SeedPlatformAdminAsync(
//        ApplicationDbContext db,
//        IDateTimeProvider clock,
//        IPasswordHasher<User> passwordHasher)
//    {
//        var admin = await EnsureUserAsync(
//            db,
//            PlatformAdminEmail,
//            clock,
//            passwordHasher);

//        var platformRoleId = await GetRoleIdAsync(
//            db,
//            Permissions.PlatformRoles.PlatformAdmin);

//        var hasRole = await db.UserRoles.AnyAsync(
//            userRole =>
//                userRole.UserId == admin.Id &&
//                userRole.RoleId == platformRoleId);

//        if (hasRole)
//        {
//            return;
//        }

//        db.UserRoles.Add(
//            new IdentityUserRole<Guid>
//            {
//                UserId = admin.Id,
//                RoleId = platformRoleId
//            });

//        await db.SaveChangesAsync();
//    }

//    // ------------------------------------------------------------
//    // Demo tenants
//    // ------------------------------------------------------------

//    private static async Task<Guid> EnsureDemoTenantAsync(
//        ApplicationDbContext db,
//        string tenantName,
//        string slug)
//    {
//        // Tenant is soft-deletable, so this lookup intentionally
//        // includes deleted tenants to avoid slug conflicts.
//        var tenant = await db.Tenants
//            .IgnoreQueryFilters()
//            .FirstOrDefaultAsync(existing => existing.Slug == slug);

//        if (tenant is not null)
//        {
//            if (tenant.IsDeleted)
//            {
//                throw new InvalidOperationException(
//                    $"Demo tenant '{slug}' exists but is soft-deleted. " +
//                    "Restore it deliberately or choose another slug.");
//            }

//            return tenant.Id;
//        }

//        tenant = Domain.Entities.Tenants.Tenant.Create(
//            tenantName,
//            slug);

//        tenant.ChangeStatus(TenantStatus.Active);

//        db.Tenants.Add(tenant);
//        await db.SaveChangesAsync();

//        return tenant.Id;
//    }

//    // ------------------------------------------------------------
//    // Demo memberships
//    // ------------------------------------------------------------

//    private static async Task SeedMembershipMatrixAsync(
//        ApplicationDbContext db,
//        IDateTimeProvider clock,
//        IPasswordHasher<User> passwordHasher,
//        Guid tenantAId,
//        Guid tenantBId)
//    {
//        var managerA = await EnsureUserAsync(
//            db,
//            "dev.manager1@example.com",
//            clock,
//            passwordHasher);

//        var kitchenB = await EnsureUserAsync(
//            db,
//            "dev.kitchen.b@example.com",
//            clock,
//            passwordHasher);

//        var multiTenantUser = await EnsureUserAsync(
//            db,
//            "dev.multitenant@example.com",
//            clock,
//            passwordHasher);

//        await EnsureMembershipAsync(
//            db,
//            tenantAId,
//            managerA.Id,
//            Permissions.TenantRoles.Manager);

//        await EnsureMembershipAsync(
//            db,
//            tenantBId,
//            kitchenB.Id,
//            Permissions.TenantRoles.Kitchen);

//        // Same user, different tenant memberships and roles.
//        await EnsureMembershipAsync(
//            db,
//            tenantAId,
//            multiTenantUser.Id,
//            Permissions.TenantRoles.Manager);

//        await EnsureMembershipAsync(
//            db,
//            tenantBId,
//            multiTenantUser.Id,
//            Permissions.TenantRoles.Kitchen);
//    }

//    private static async Task EnsureMembershipAsync(
//        ApplicationDbContext db,
//        Guid tenantId,
//        Guid userId,
//        string roleName)
//    {
//        var roleId = await GetRoleIdAsync(db, roleName);

//        // Include all records so a removed or otherwise hidden membership
//        // cannot be mistaken for a membership that does not exist.
//        var existingMembership = await db.StaffMemberships
//            .IgnoreQueryFilters()
//            .FirstOrDefaultAsync(
//                membership =>
//                    membership.TenantId == tenantId &&
//                    membership.UserId == userId);

//        if (existingMembership is not null)
//        {
//            if (existingMembership.RoleId != roleId)
//            {
//                throw new InvalidOperationException(
//                    $"User '{userId}' already has a membership in tenant " +
//                    $"'{tenantId}' with a different role. Resolve the " +
//                    "existing membership explicitly.");
//            }

//            // Existing membership is left unchanged.
//            // In particular, this method does not silently reactivate
//            // removed memberships.
//            return;
//        }

//        db.StaffMemberships.Add(
//            Domain.Entities.Tenants.StaffMembership.Create(
//                tenantId,
//                userId,
//                roleId));

//        await db.SaveChangesAsync();
//    }

//    // ------------------------------------------------------------
//    // Restaurants
//    // ------------------------------------------------------------

//    private static async Task SeedRestaurantsAsync(
//        ApplicationDbContext db,
//        Guid[] tenantIds)
//    {
//        for (var i = 0; i < DemoTenants.Length; i++)
//        {
//            var tenantId = tenantIds[i];
//            var spec = DemoTenants[i];

//            var restaurantExists = await db.Restaurants
//                .IgnoreQueryFilters()
//                .AnyAsync(restaurant =>
//                    restaurant.TenantId == tenantId &&
//                    !restaurant.IsDeleted);

//            if (restaurantExists)
//            {
//                continue;
//            }

//            var address = Domain.ValueObjects.Address.Create(
//                spec.AddressLine1,
//                spec.City,
//                "Pakistan");

//            var restaurant =
//                Domain.Entities.Restaurants.Restaurant.Create(
//                    tenantId,
//                    spec.RestaurantName,
//                    address);

//            db.Restaurants.Add(restaurant);
//        }

//        await db.SaveChangesAsync();
//    }

//    // ------------------------------------------------------------
//    // Identity users
//    // ------------------------------------------------------------

//    private static async Task<User> EnsureUserAsync(
//        ApplicationDbContext db,
//        string email,
//        IDateTimeProvider clock,
//        IPasswordHasher<User> passwordHasher)
//    {
//        var normalizedEmail = email.ToUpperInvariant();

//        var existingUser = await db.Users
//            .IgnoreQueryFilters()
//            .FirstOrDefaultAsync(
//                user => user.NormalizedEmail == normalizedEmail);

//        if (existingUser is not null)
//        {
//            return existingUser;
//        }

//        var user = new User
//        {
//            Id = Guid.NewGuid(),
//            UserName = email,
//            NormalizedUserName = normalizedEmail,
//            Email = email,
//            NormalizedEmail = normalizedEmail,
//            EmailConfirmed = true,
//            CreatedAt = clock.UtcNow,
//            SecurityStamp = Guid.NewGuid().ToString(),
//            ConcurrencyStamp = Guid.NewGuid().ToString()
//        };

//        user.PasswordHash = passwordHasher.HashPassword(
//            user,
//            DevPassword);

//        db.Users.Add(user);
//        await db.SaveChangesAsync();

//        return user;
//    }

//    // ------------------------------------------------------------
//    // Shared helpers
//    // ------------------------------------------------------------

//    private static async Task<Guid> GetRoleIdAsync(
//        ApplicationDbContext db,
//        string roleName)
//    {
//        var normalizedName = roleName.ToUpperInvariant();

//        var role = await db.Roles
//            .IgnoreQueryFilters()
//            .FirstOrDefaultAsync(
//                existing => existing.NormalizedName == normalizedName);

//        if (role is null)
//        {
//            throw new InvalidOperationException(
//                $"Required role '{roleName}' was not found. " +
//                "Seed roles before seeding users and memberships.");
//        }

//        return role.Id;
//    }
//}