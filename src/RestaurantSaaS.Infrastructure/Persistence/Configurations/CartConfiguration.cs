
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Carts;
using RestaurantSaaS.Domain.Entities.Customers;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.CustomerId)
            .IsRequired(false);

        builder.Property(c => c.SessionId)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.ExpiresAt)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false);

        // Guest-cart lookup within a tenant.
        // Non-unique because a session may have historical carts.
        builder.HasIndex(c => new { c.TenantId, c.SessionId })
            .HasDatabaseName("IX_Carts_TenantId_SessionId")
            .HasFilter("[SessionId] IS NOT NULL");

        // Find a customer's active carts within a tenant.
        builder.HasIndex(c => new
        {
            c.TenantId,
            c.CustomerId,
            c.Status
        })
            .HasDatabaseName("IX_Carts_TenantId_CustomerId_Status")
            .HasFilter("[CustomerId] IS NOT NULL");

        // Find expired active carts for scheduled cleanup.
        builder.HasIndex(c => new
        {
            c.TenantId,
            c.Status,
            c.ExpiresAt
        })
            .HasDatabaseName("IX_Carts_TenantId_Status_ExpiresAt");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer deletion must not delete the cart.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Navigation(c => c.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}