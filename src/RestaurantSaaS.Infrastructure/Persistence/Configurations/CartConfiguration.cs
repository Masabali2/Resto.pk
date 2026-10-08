using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Carts;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SessionId).HasMaxLength(64);

        // Guest-cart lookup path and expiry cleanup job.
        builder.HasIndex(c => c.SessionId);
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Customer)
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.SetNull); // losing a customer must not destroy carts
    }
}
