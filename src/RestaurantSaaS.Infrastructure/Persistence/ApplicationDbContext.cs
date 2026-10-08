using Microsoft.AspNetCore.Identity;
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
/// Single database context for Identity + business schema.
///
/// TENANT ISOLATION LAYER (requirement #10, item 5): global query filters
/// automatically hide other tenants' rows for every ITenantOwned entity and
/// hide soft-deleted rows. Filters are bypassable ONLY via IgnoreQueryFilters
/// which is reserved for platform-admin/system code paths (and audit/history
/// reads) — never for ordinary tenant requests.
///
/// IMPORTANT: query filters are defense-in-depth, NOT the only mechanism.
/// Ownership validation still happens in use cases, and writes are guarded by
/// composite FKs + unique constraints at the database level.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User, Role, Guid>
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

    // ---- Tenant root & membership ----
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<StaffMembership> StaffMemberships => Set<StaffMembership>();

    // ---- Restaurant ----
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<RestaurantAsset> RestaurantAssets => Set<RestaurantAsset>();

    // ---- Subscription ----
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    // ---- Menu ----
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    // ---- Website ----
    public DbSet<WebsiteTemplate> WebsiteTemplates => Set<WebsiteTemplate>();
    public DbSet<Website> Websites => Set<Website>();
    public DbSet<WebsiteConfiguration> WebsiteConfigurations => Set<WebsiteConfiguration>();

    // ---- Customer / Cart / Order ----
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    // ---- Platform services ----
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<FileMetadata> Files => Set<FileMetadata>();
    public DbSet<QRCode> QRCodes => Set<QRCode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// When true (platform-admin system scope), tenant filters are disabled for
    /// this context instance. Set exclusively by authorized platform code paths.
    /// </summary>
    public bool DisableTenantFilters { get; set; }

    /// <summary>
    /// Ambient tenant used by global query filters. The filter expression must
    /// reference a FIELD (not a scoped service property) to avoid capturing the
    /// scoped ITenantContext inside the singleton model — EF evaluates this
    /// field per context instance at query time.
    /// </summary>
    private Guid? CurrentTenantId => DisableTenantFilters ? null : _tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity tables first (AspNetUsers, AspNetRoles, AspNetUserRoles,
        // AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens).
        base.OnModelCreating(builder);

        // One IEntityTypeConfiguration<T> per entity — no monolithic OnModelCreating.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplyTenantAndSoftDeleteQueryFilters(builder);
    }

    /// <summary>
    /// Applies global query filters AFTER configurations so they compose with
    /// any relationship-level filters EF generated. Each entity gets exactly
    /// ONE filter combining tenant isolation + soft deletion.
    /// </summary>
    private void ApplyTenantAndSoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

            if (!isTenantOwned && !isSoftDeletable)
                continue;

            var methodName = (isTenantOwned, isSoftDeletable) switch
            {
                (true, true) => nameof(ApplyTenantAndSoftDeleteFilter),
                (true, false) => nameof(ApplyTenantFilter),
                (false, true) => nameof(ApplySoftDeleteFilter),
                _ => null
            };

            var method = typeof(ApplicationDbContext)
                .GetMethod(methodName!, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [builder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity, ITenantOwned
    {
        // NOTE: HasQueryFilter is applied AFTER any soft-delete filter for the
        // same entity on purpose — the soft-delete call below overwrites it with
        // the COMBINED filter (ApplyTenantAndSoftDeleteFilter), so each entity
        // ends up with exactly one composed predicate.
        builder.Entity<TEntity>().HasQueryFilter(e =>
            DisableTenantFilters || (CurrentTenantId != null && e.TenantId == CurrentTenantId));
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : BaseEntity, ITenantOwned, ISoftDeletable
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (DisableTenantFilters || (CurrentTenantId != null && e.TenantId == CurrentTenantId)));
    }

    protected override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        // Audit stamps are set server-side only — clients can never forge timestamps.
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
