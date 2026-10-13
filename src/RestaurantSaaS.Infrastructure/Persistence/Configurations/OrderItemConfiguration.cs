using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Orders;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(18, 4);
        builder.Property(i => i.LineTotal).HasPrecision(18, 4);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.HasIndex(i => i.OrderId);

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade); // items belong to their order aggregate

        // NULLABLE + SET NULL: menu items may be purged later; the historical
        // snapshot (ItemName/UnitPrice) on this row is the source of truth.
        builder.HasOne<RestaurantSaaS.Domain.Entities.Menus.MenuItem>()
            .WithMany()
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
