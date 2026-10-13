
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Orders;
using RestaurantSaaS.Domain.Entities.Menus;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_OrderItems_UnitPrice",
                "[UnitPrice] >= 0");

            table.HasCheckConstraint(
                "CK_OrderItems_Quantity",
                "[Quantity] >= 1 AND [Quantity] <= 999");

            table.HasCheckConstraint(
                "CK_OrderItems_LineTotal",
                "[LineTotal] >= 0 AND [LineTotal] = [UnitPrice] * [Quantity]");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderId)
            .IsRequired();

        builder.Property(i => i.MenuItemId)
            .IsRequired(false);

        builder.Property(i => i.ItemName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.LineTotal)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("IX_OrderItems_OrderId");

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}