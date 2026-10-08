using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Orders;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.CustomerPhone).HasMaxLength(30);
        builder.Property(o => o.CustomerEmail).HasMaxLength(256);
        builder.Property(o => o.SpecialInstructions).HasMaxLength(2000);

        builder.Property(o => o.Subtotal).HasPrecision(18, 4);
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 4);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 4);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 4);
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        // CONCURRENCY (requirement #31): SQL Server rowversion on the single
        // most-contended aggregate — justified, not applied everywhere.
        builder.Property(o => o.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // SECURITY INVARIANT: order numbers unique WITHIN a tenant.
        builder.HasIndex(o => new { o.TenantId, o.OrderNumber }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => new { o.RestaurantId, o.CreatedAt });
        builder.HasIndex(o => o.CreatedAt);

        // Composite FK (TenantId, RestaurantId) -> Restaurants(TenantId, Id):
        // an order can NEVER reference another tenant's restaurant at DB level.
        builder.HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => new { o.TenantId, o.RestaurantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.SetNull); // orders keep working after customer soft-delete
    }
}
