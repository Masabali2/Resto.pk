
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Entities.Audit;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Customers;
using RestaurantSaaS.Domain.Entities.Files;
using RestaurantSaaS.Domain.Entities.Identity;
using RestaurantSaaS.Domain.Entities.Menus;
using RestaurantSaaS.Domain.Entities.Notifications;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.QRCodes;
using RestaurantSaaS.Domain.Entities.Restaurants;
using RestaurantSaaS.Domain.Entities.Subscriptions;
using RestaurantSaaS.Domain.Entities.Tenants;
using RestaurantSaaS.Domain.Entities.Websites;

namespace RestaurantSaaS.Infrastructure.Persistence;

/// <summary>
/// Main EF Core database context for ASP.NET Core Identity
/// and restaurant SaaS business entities.
///
/// Tenant-owned entities are filtered by the current tenant.
/// Soft-deletable entities exclude deleted records.
/// Dependent entities follow the visibility of their required parents.
///
/// Query filters provide defense-in-depth, not complete authorization.
/// Application use cases must validate ownership and permissions.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<User, Role, Guid>
{
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider)
        : base(options)
    {
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // Tenant and membership
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<StaffMembership> StaffMemberships => Set<StaffMembership>();

    public DbSet<EmailVerificationOtp> EmailVerificationOtps =>
        Set<EmailVerificationOtp>();

    // Restaurant
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<RestaurantAsset> RestaurantAssets => Set<RestaurantAsset>();

    // Subscription
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    // Menu
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    // Website
    public DbSet<WebsiteTemplate> WebsiteTemplates => Set<WebsiteTemplate>();
    public DbSet<Website> Websites => Set<Website>();
    public DbSet<WebsiteConfiguration> WebsiteConfigurations
        => Set<WebsiteConfiguration>();

    // Customer, cart, and order
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories
        => Set<OrderStatusHistory>();

    // Platform services
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FileMetadata> Files => Set<FileMetadata>();
    public DbSet<QRCode> QRCodes => Set<QRCode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Enables tenant-filter bypass for explicitly authorized system operations.
    /// This must never be controlled directly by untrusted request input.
    /// </summary>
    public bool DisableTenantFilters { get; set; }

    /// <summary>
    /// Reads the tenant from this context instance at query time.
    /// </summary>
    private Guid? CurrentTenantId =>
        DisableTenantFilters ? null : _tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(64);

            entity.Property(x => x.CreatedByIp)
                .HasMaxLength(45);

            entity.Property(x => x.RevokedByIp)
                .HasMaxLength(45);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Apply all IEntityTypeConfiguration<TEntity> classes.
        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        // Apply filters to tenant-owned and/or soft-deletable entities.
        ApplyTenantAndSoftDeleteQueryFilters(builder);

        // Ensure dependent entities follow their required parent's visibility.
        ApplyDependentEntityQueryFilters(builder);
    }

    private void ApplyTenantAndSoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var entityClrType = entityType.ClrType;

            // Skip Identity entities and other non-business entities.
            if (!typeof(BaseEntity).IsAssignableFrom(entityClrType))
                continue;

            var isTenantOwned =
                typeof(ITenantOwned).IsAssignableFrom(entityClrType);

            var isSoftDeletable =
                typeof(ISoftDeletable).IsAssignableFrom(entityClrType);

            if (!isTenantOwned && !isSoftDeletable)
                continue;

            var methodName = (isTenantOwned, isSoftDeletable) switch
            {
                (true, true) =>
                    nameof(ApplyTenantAndSoftDeleteFilter),

                (true, false) =>
                    nameof(ApplyTenantFilter),

                (false, true) =>
                    nameof(ApplySoftDeleteFilter),

                _ => throw new InvalidOperationException(
                    $"Unsupported filter configuration for {entityClrType.Name}.")
            };

            var method = typeof(ApplicationDbContext).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method is null)
            {
                throw new InvalidOperationException(
                    $"Could not find query filter method '{methodName}'.");
            }

            method
                .MakeGenericMethod(entityClrType)
                .Invoke(this, [builder]);
        }
    }

    /// <summary>
    /// Applies compatible visibility rules to dependent entities whose
    /// required parent already has a global query filter.
    ///
    /// These predicates assume the named parent navigation properties exist
    /// and each parent implements the stated tenant/deletion properties.
    /// </summary>
    private void ApplyDependentEntityQueryFilters(ModelBuilder builder)
    {
        // CartItem -> Cart
        builder.Entity<CartItem>().HasQueryFilter(item =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             item.Cart.TenantId == CurrentTenantId));

        // OrderItem -> Order
        builder.Entity<OrderItem>().HasQueryFilter(item =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             item.Order.TenantId == CurrentTenantId));

        // OrderStatusHistory -> Order
        builder.Entity<OrderStatusHistory>().HasQueryFilter(history =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             history.Order.TenantId == CurrentTenantId));

        // RestaurantAsset -> Restaurant
        builder.Entity<RestaurantAsset>().HasQueryFilter(asset =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             asset.Restaurant.TenantId == CurrentTenantId));

        // WebsiteConfiguration -> Website
        builder.Entity<WebsiteConfiguration>().HasQueryFilter(configuration =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             configuration.Website.TenantId == CurrentTenantId));
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity, ITenantOwned
    {
        builder.Entity<TEntity>().HasQueryFilter(entity =>
            DisableTenantFilters ||
            (CurrentTenantId != null &&
             entity.TenantId == CurrentTenantId));
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(entity =>
            !entity.IsDeleted);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(
        ModelBuilder builder)
        where TEntity : BaseEntity, ITenantOwned, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(entity =>
            !entity.IsDeleted &&
            (DisableTenantFilters ||
             (CurrentTenantId != null &&
              entity.TenantId == CurrentTenantId)));
    }

    // ------------------------------------------------------------
    // Audit timestamps
    // ------------------------------------------------------------

    private void ApplyAuditStamps()
    {
        var now = _dateTimeProvider.UtcNow;

        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException(
                "IDateTimeProvider.UtcNow must return a UTC DateTime.");
        }

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    // Creation timestamps are immutable after insertion.
                    entry.Property(entity => entity.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditStamps();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditStamps();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }
}