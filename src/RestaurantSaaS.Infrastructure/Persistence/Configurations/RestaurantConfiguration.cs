using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantSaaS.Domain.Entities.Restaurants;

namespace RestaurantSaaS.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.Phone).HasMaxLength(30);
        builder.Property(r => r.Email).HasMaxLength(256);
        builder.Property(r => r.City).HasMaxLength(100);
        builder.Property(r => r.Country).HasMaxLength(100);
        builder.Property(r => r.TimeZone).HasMaxLength(64).IsRequired();

        // Address value object -> owned columns (no separate table).
        builder.OwnsOne(r => r.Address, a =>
        {
            a.Property(x => x.Line1).HasMaxLength(200).HasColumnName("AddressLine1");
            a.Property(x => x.Line2).HasMaxLength(200).HasColumnName("AddressLine2");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("AddressCity");
            a.Property(x => x.State).HasMaxLength(100).HasColumnName("AddressState");
            a.Property(x => x.PostalCode).HasMaxLength(20).HasColumnName("AddressPostalCode");
            a.Property(x => x.Country).HasMaxLength(100).HasColumnName("AddressCountry");
        });

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.Status });

        // TENANT INTEGRITY ANCHOR (requirement #11):
        // Composite unique key (TenantId, Id) lets dependent tables declare a
        // composite FK (TenantId, RestaurantId) so the database itself rejects
        // rows that point at another tenant's restaurant.
        builder.HasIndex(r => new { r.TenantId, r.Id }).IsUnique();

        builder.HasOne(r => r.Tenant)
            .WithMany(t => t.Restaurants)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict); // never cascade-delete an entire tenant's restaurants

        builder.Navigation(r => r.Assets).AutoInclude(false);
    }
}
