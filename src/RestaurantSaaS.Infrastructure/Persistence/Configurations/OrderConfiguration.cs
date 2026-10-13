
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Orders;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_Subtotal",
                "[Subtotal] >= 0");

            table.HasCheckConstraint(
                "CK_Orders_DiscountAmount",
                "[DiscountAmount] >= 0 AND [DiscountAmount] <= [Subtotal]");

            table.HasCheckConstraint(
                "CK_Orders_TaxAmount",
                "[TaxAmount] >= 0");

            table.HasCheckConstraint(
                "CK_Orders_TotalAmount",
                "[TotalAmount] >= 0 AND " +
                "[TotalAmount] = [Subtotal] - [DiscountAmount] + [TaxAmount]");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.TenantId)
            .IsRequired();

        builder.Property(o => o.RestaurantId)
            .IsRequired();

        builder.Property(o => o.CustomerId)
            .IsRequired(false);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(o => o.CustomerName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(o => o.CustomerPhone)
            .HasMaxLength(30);

        builder.Property(o => o.CustomerEmail)
            .HasMaxLength(254);

        builder.Property(o => o.SpecialInstructions)
            .HasMaxLength(2000);

        builder.Property(o => o.Subtotal)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.DiscountAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.TaxAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(o => o.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(o => o.CompletedAt);

        builder.Property(o => o.CancelledAt);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.UpdatedAt);

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.HasIndex(o => new { o.TenantId, o.OrderNumber })
            .IsUnique()
            .HasDatabaseName("UX_Orders_TenantId_OrderNumber");

        builder.HasIndex(o => new
        {
            o.TenantId,
            o.RestaurantId,
            o.CreatedAt
        })
            .HasDatabaseName(
                "IX_Orders_TenantId_RestaurantId_CreatedAt");

        builder.HasIndex(o => new
        {
            o.TenantId,
            o.Status,
            o.CreatedAt
        })
            .HasDatabaseName(
                "IX_Orders_TenantId_Status_CreatedAt");

        builder.HasOne(o => o.Restaurant)
            .WithMany()
            .HasForeignKey(o => new { o.TenantId, o.RestaurantId })
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}