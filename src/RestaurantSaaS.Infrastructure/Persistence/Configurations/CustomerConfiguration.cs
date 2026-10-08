using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Customers;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.Email).HasMaxLength(256);

        // Lookup by contact within a tenant (duplicate detection at checkout).
        builder.HasIndex(c => new { c.TenantId, c.Phone });
        builder.HasIndex(c => new { c.TenantId, c.Email });

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        // Soft-deleted customers remain for order history; orders snapshot the
        // customer fields anyway, so no cascade is ever needed.
    }
}
