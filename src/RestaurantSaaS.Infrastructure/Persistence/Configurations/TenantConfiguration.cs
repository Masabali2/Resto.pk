using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Tenants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(63).IsRequired(); // DNS label-friendly

        // SECURITY: tenant slug is globally unique (subdomain identity).
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.Status);
    }
}
